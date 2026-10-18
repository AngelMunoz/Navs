namespace Navs.Router

open System
open System.Buffers
open System.Collections.Concurrent
open System.Collections.Frozen
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

open Microsoft.Extensions.Logging

open IcedTasks
open FsToolkit.ErrorHandling

open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser
open UrlTemplates.UrlTemplate

open Navs

module Dictionary =

  let inline tryGetValue (dict: IReadOnlyDictionary<'Key, 'Value>) (key: 'Key) =
    let mutable value = Unchecked.defaultof<'Value>

    if dict.TryGetValue(key, &value) then
      ValueSome value
    else
      ValueNone


type RouteDisposables(?logger: ILogger) =
  let mutable disposables: ResizeArray<IDisposable> option = None

  interface IDisposableBag with
    member _.AddDisposable disposable =
      match disposables with
      | Some items -> items.Add disposable
      | None -> disposables <- Some(ResizeArray [ disposable ])

    member _.Dispose() =
      match disposables with
      | Some items ->
        for disposable in items do
          try
            disposable.Dispose()
          with error ->
            logger
            |> Option.iter(fun log ->
              log.LogError(error, "Failed to dispose a route resource")
            )

        disposables <- None
      | None -> ()

type CompiledRoute<'View> = {
  Definition: RouteDefinition<'View>
  Template: UrlTemplate
  Order: int
  Score: int
}

type CacheEntry<'View> = {
  Context: RouteContext
  View: 'View
  CreatedAt: DateTimeOffset
}

type UrlBuffer() =
  let pool = ArrayPool<char>.Shared
  let mutable buffer = pool.Rent 256
  let mutable length = 0
  let mutable disposed = false

  let ensure extra =
    let required = length + extra

    if required > buffer.Length then
      let size = max required (buffer.Length * 2)
      let next = pool.Rent size
      Array.Copy(buffer, next, length)
      pool.Return buffer
      buffer <- next

  member _.Append(value: string) =
    ensure value.Length
    value.CopyTo(0, buffer, length, value.Length)
    length <- length + value.Length

  member _.Append(value: char) =
    ensure 1
    buffer[length] <- value
    length <- length + 1

  member _.ToStringValue() =
    System.String(ReadOnlySpan<char>(buffer, 0, length))

  interface IDisposable with
    member _.Dispose() =
      if not disposed then
        disposed <- true
        pool.Return buffer

type GuardStage =
  | Allowed
  | Halted of NavigationError
  | RedirectTo of string

type RedirectResolution<'View> = {
  Route: CompiledRoute<'View>
  Context: RouteContext
  Redirects: string list
  Count: int
}

type ActivatedNavigation<'View> = {
  Route: CompiledRoute<'View>
  Context: RouteContext
  View: 'View
  Redirects: string list
}

module Redirects =

  let inline tryFollow
    (maxRedirects: int)
    (url: string)
    (chain: string list)
    (count: int)
    =
    if count >= maxRedirects then
      Error(RedirectLoop(List.rev(url :: chain)))
    else
      Ok(Url url, url :: chain, count + 1)

module Views =

  let inline run
    (definition: RouteDefinition<'View>)
    (context: RouteContext)
    (navigable: INavigable)
    (token: CancellationToken)
    : ValueTask<'View> =
    match definition.view with
    | SyncViewWork work -> work context navigable
    | AsyncViewWork work -> ValueTask<'View>(work context navigable token)

module Guards =

  let inline runOne
    (work: GuardWork)
    (active: RouteContext voption)
    (next: RouteContext)
    (token: CancellationToken)
    : ValueTask<GuardResult> =
    match work with
    | SyncGuardWork guard -> guard active next
    | AsyncGuardWork guard -> ValueTask<GuardResult>(guard active next token)

  let run
    (guards: GuardWork list)
    (active: RouteContext voption)
    (next: RouteContext)
    : CancellableValueTask<GuardResult> =
    if List.isEmpty guards then
      fun _ -> ValueTask.FromResult Continue
    else
      cancellableValueTask {
        let! token = CancellableValueTask.getCancellationToken()
        let mutable remaining = guards
        let mutable result = Continue

        while result = Continue && not(List.isEmpty remaining) do
          match remaining with
          | [] -> ()
          | guard :: rest ->
            remaining <- rest

            if token.IsCancellationRequested then
              result <- Cancel
            else
              match! runOne guard active next with
              | Continue -> ()
              | other -> result <- other

        return result
      }

  let runDeactivation
    (definition: RouteDefinition<'View> voption)
    (activeRoute: RouteContext voption)
    (context: RouteContext)
    : CancellableValueTask<GuardResult> =
    match definition with
    | ValueSome routeDefinition ->
      run routeDefinition.canDeactivate activeRoute context
    | ValueNone -> fun _ -> ValueTask.FromResult Continue

  let runStages
    (globalGuards: GuardWork list)
    (activeRoute: RouteContext voption)
    (activeDefinition: RouteDefinition<'View> voption)
    (route: CompiledRoute<'View>)
    (context: RouteContext)
    : CancellableValueTask<GuardStage> =
    cancellableValueTask {
      match! run globalGuards activeRoute context with
      | Stop -> return Halted(GuardStopped route.Definition.name)
      | Cancel -> return Halted Cancelled
      | Redirect url -> return RedirectTo url
      | Continue ->
        match! runDeactivation activeDefinition activeRoute context with
        | Stop ->
          return
            Halted(
              GuardStopped(
                activeDefinition
                |> ValueOption.map _.name
                |> ValueOption.defaultValue route.Definition.name
              )
            )
        | Cancel -> return Halted Cancelled
        | Redirect url -> return RedirectTo url
        | Continue ->
          match! run route.Definition.canActivate activeRoute context with
          | Stop -> return Halted(GuardStopped route.Definition.name)
          | Cancel -> return Halted Cancelled
          | Redirect url -> return RedirectTo url
          | Continue -> return Allowed
    }

module ViewCache =

  let inline isActive
    (activeRoute: RouteContext voption)
    (context: RouteContext)
    =
    activeRoute |> ValueOption.exists(fun active -> active.Path = context.Path)

  let inline isExpired
    (now: DateTimeOffset)
    (ttl: TimeSpan option)
    (entry: CacheEntry<'View>)
    =
    match ttl with
    | Some lifetime -> now - entry.CreatedAt > lifetime
    | None -> false

  let tryGet
    (cache: ConcurrentDictionary<string, CacheEntry<'View>>)
    (policy: CachePolicy)
    (context: RouteContext)
    : 'View voption =
    match policy with
    | NoCache -> ValueNone
    | Cache(_, ttl) ->
      match Dictionary.tryGetValue cache context.Path with
      | ValueSome entry when isExpired DateTimeOffset.UtcNow ttl entry ->
        cache.TryRemove(context.Path) |> ignore
        entry.Context.Disposables.Dispose()
        ValueNone
      | ValueSome entry -> ValueSome entry.View
      | ValueNone -> ValueNone

  let inline store
    ([<InlineIfLambda>] isActive: RouteContext -> bool)
    (cache: ConcurrentDictionary<string, CacheEntry<'View>>)
    (policy: CachePolicy)
    (context: RouteContext)
    (view: 'View)
    =
    match policy with
    | Cache(maxEntries, _) when maxEntries > 0 ->
      if cache.Count >= maxEntries then
        cache
        |> Seq.tryHead
        |> Option.iter(fun pair ->
          match cache.TryRemove pair.Key with
          | true, evicted ->
            if not(isActive evicted.Context) then
              evicted.Context.Disposables.Dispose()
          | false, _ -> ()
        )

      cache.TryAdd(
        context.Path,
        {
          Context = context
          View = view
          CreatedAt = DateTimeOffset.UtcNow
        }
      )
      |> ignore
    | _ -> ()

module HistoryNavigation =

  let inline sameUrlEntry
    (history: IHistoryStore)
    (policy: SameUrlPolicy)
    (path: string)
    (current: HistoryEntry)
    =
    match policy with
    | Ignore -> current
    | Replace -> history.Replace path
    | Push -> history.Push path

  let resolveEntry
    (history: IHistoryStore)
    (policy: SameUrlPolicy)
    (historyIndex: int option)
    (context: RouteContext)
    (redirects: string list)
    : HistoryEntry =
    match historyIndex with
    | Some index ->
      history.Commit index
      |> Option.defaultWith(fun () -> history.Replace context.Path)
    | None ->
      match history.Current with
      | None -> history.Replace context.Path
      | Some current when current.Url = context.Path ->
        sameUrlEntry history policy context.Path current
      | Some _ when not(List.isEmpty redirects) -> history.Replace context.Path
      | Some _ -> history.Push context.Path

module RouteLifetime =

  let inline disposeIfUncached
    (definition: RouteDefinition<'View> voption)
    (route: RouteContext voption)
    =
    match definition, route with
    | ValueSome { cache = NoCache }, ValueSome previous ->
      previous.Disposables.Dispose()
    | _ -> ()

module UrlBuilder =

  let inline encode (value: string) = Uri.EscapeDataString value

  let valueToString (name: string) (value: obj) =
    if Object.ReferenceEquals(value, null) then
      Error $"Required route parameter %s{name} was not provided"
    else
      match value.ToString() with
      | null -> Error $"Required route parameter %s{name} was not provided"
      | text -> Ok text

  let inline tryOptionalParameter (parameters: RouteParams) (name: string) =
    Dictionary.tryGetValue parameters name
    |> ValueOption.map(valueToString name)

  let inline tryParameter (parameters: RouteParams) (name: string) =
    tryOptionalParameter parameters name
    |> ValueOption.defaultWith(fun () ->
      Error $"Required route parameter %s{name} was not provided"
    )

  let build (template: UrlTemplate) (parameters: RouteParams) =
    let parameters =
      parameters
      |> ValueOption.ofNull
      |> ValueOption.defaultWith(fun () ->
        Dictionary<string, obj>() :> RouteParams
      )

    use writer = new UrlBuffer()
    let mutable error: string voption = ValueNone
    let mutable firstSegment = true
    let mutable firstQuery = true

    let appendSegment (value: string) =
      if firstSegment then
        firstSegment <- false
      else
        writer.Append '/'

      writer.Append(encode value)

    let appendQuery (name: string) (value: string) =
      writer.Append(if firstQuery then '?' else '&')
      firstQuery <- false
      writer.Append(encode name)
      writer.Append '='
      writer.Append(encode value)

    for segment in template.Segments do
      if error.IsNone then
        match segment with
        | Plain value -> appendSegment value
        | ParamSegment(name, _) ->
          match tryParameter parameters name with
          | Ok text -> appendSegment text
          | Error message -> error <- ValueSome message

    for item in template.Query do
      if error.IsNone then
        match item with
        | Required(name, _) ->
          match tryParameter parameters name with
          | Ok text -> appendQuery name text
          | Error message -> error <- ValueSome message
        | Optional(name, _) ->
          match tryOptionalParameter parameters name with
          | ValueSome(Ok text) -> appendQuery name text
          | ValueSome(Error message) -> error <- ValueSome message
          | ValueNone -> ()

    if error.IsNone then
      match template.Hash with
      | ValueSome value ->
        writer.Append '#'
        writer.Append(encode value)
      | ValueNone -> ()

    match error with
    | ValueSome message -> Error message
    | ValueNone -> Ok(writer.ToStringValue())

module RouteSignature =

  let typeKey =
    function
    | String -> "string"
    | Int -> "int"
    | Float -> "float"
    | Bool -> "bool"
    | Guid -> "guid"
    | Long -> "long"
    | Decimal -> "decimal"

  let ofTemplate (template: UrlTemplate) =
    let segments, segmentScore =
      template.Segments
      |> List.mapFold
        (fun score segment ->
          match segment with
          | Plain value -> $"s:{value}", score + 10
          | ParamSegment(_, String) -> "p:string", score + 2
          | ParamSegment(_, tipe) -> $"p:{typeKey tipe}", score + 5
        )
        0

    let query =
      template.Query
      |> List.map(
        function
        | Required(_, tipe) -> $"r:{typeKey tipe}"
        | Optional(_, tipe) -> $"o:{typeKey tipe}"
      )

    let hash =
      template.Hash
      |> ValueOption.map(fun value -> $"h:{value}")
      |> ValueOption.defaultValue ""

    String.concat "|" (segments @ query @ [ hash ]),
    segmentScore + template.Segments.Length

module RouteValidation =

  let validate
    (names: HashSet<string>)
    (patterns: HashSet<string>)
    (signatures: Dictionary<string, string>)
    (order: int)
    (definition: RouteDefinition<'View>)
    : RouteTableError list * CompiledRoute<'View> option =
    let nameErrors =
      if String.IsNullOrWhiteSpace definition.name then
        [ EmptyRouteName ]
      elif not(names.Add definition.name) then
        [ DuplicateRouteName definition.name ]
      else
        []

    let patternErrors =
      if not(patterns.Add definition.pattern) then
        [ DuplicatePattern definition.pattern ]
      else
        []

    match UrlTemplate.ofString definition.pattern with
    | Error message ->
      nameErrors
      @ patternErrors
      @ [ InvalidPattern(definition.pattern, message) ],
      None
    | Ok template ->
      if not(List.isEmpty nameErrors) || not(List.isEmpty patternErrors) then
        nameErrors @ patternErrors, None
      else
        let signature, score = RouteSignature.ofTemplate template

        match Dictionary.tryGetValue signatures signature with
        | ValueSome existing ->
          [ AmbiguousPattern(existing, definition.pattern) ], None
        | ValueNone ->
          signatures[signature] <- definition.pattern

          [],
          Some {
            Definition = definition
            Template = template
            Order = order
            Score = score
          }

type RouteTable<'View> = {
  Routes: CompiledRoute<'View>[]
  ByName: FrozenDictionary<string, CompiledRoute<'View>>
  ByFirstStaticSegment: FrozenDictionary<string, CompiledRoute<'View>[]>
  DynamicRoutes: CompiledRoute<'View>[]
} with

  static member Compile(definitions: RouteDefinition<'View> list) =
    if List.isEmpty definitions then
      Error [ EmptyRoutes ]
    else
      let names = HashSet<string>()
      let patterns = HashSet<string>()
      let signatures = Dictionary<string, string>()

      let results =
        definitions
        |> List.mapi(RouteValidation.validate names patterns signatures)

      let errors = results |> List.collect fst
      let compiled = results |> List.choose snd

      if not(List.isEmpty errors) then
        Error errors
      else
        let routes =
          compiled
          |> List.sortBy(fun route ->
            -route.Score, -route.Definition.priority, route.Order
          )
          |> Array.ofList

        let byName = Dictionary<string, CompiledRoute<'View>>(routes.Length)

        let staticGroups =
          Dictionary<string, ResizeArray<CompiledRoute<'View>>>()

        let dynamicRoutes = ResizeArray<CompiledRoute<'View>>()

        for route in routes do
          byName[route.Definition.name] <- route

          match route.Template.Segments with
          | Plain first :: _ ->
            match Dictionary.tryGetValue staticGroups first with
            | ValueSome group -> group.Add route
            | ValueNone -> staticGroups[first] <- ResizeArray [ route ]
          | _ -> dynamicRoutes.Add route

        let staticByName =
          Dictionary<string, CompiledRoute<'View>[]>(staticGroups.Count)

        for pair in staticGroups do
          staticByName[pair.Key] <- pair.Value.ToArray()

        Ok {
          Routes = routes
          ByName = byName.ToFrozenDictionary()
          ByFirstStaticSegment = staticByName.ToFrozenDictionary()
          DynamicRoutes = dynamicRoutes.ToArray()
        }

  member this.Match(url: string, ?logger: ILogger) =
    match UrlParser.ofString url with
    | Error message -> Error(InvalidTarget message)
    | Ok urlInfo ->
      let firstSegment =
        match urlInfo.Segments with
        | first :: _ -> first
        | [] -> ""

      let tryFind (routes: CompiledRoute<'View>[]) =
        let mutable index = 0
        let mutable found = ValueNone

        while found.IsNone && index < routes.Length do
          let route = routes[index]

          match RouteMatcher.matchTemplate route.Template urlInfo with
          | Ok urlMatch -> found <- ValueSome(route, urlMatch)
          | Error _ -> ()

          index <- index + 1

        found

      let matched =
        Dictionary.tryGetValue this.ByFirstStaticSegment firstSegment
        |> ValueOption.bind tryFind
        |> ValueOption.orElseWith(fun () -> tryFind this.DynamicRoutes)

      match matched with
      | ValueNone -> Error(RouteNotFound url)
      | ValueSome(route, urlMatch) ->
        let context = {
          Path = url
          UrlInfo = urlInfo
          UrlMatch = urlMatch
          Disposables = new RouteDisposables(?logger = logger)
        }

        Ok(route, context)

  member this.TryBuildUrl(name: string, parameters: RouteParams) =
    match Dictionary.tryGetValue this.ByName name with
    | ValueSome route -> UrlBuilder.build route.Template parameters
    | ValueNone -> Error $"Route '{name}' was not found"

type RouteLinks<'View>
  (
    table: RouteTable<'View>,
    [<InlineIfLambda>] current: unit -> RouteContext voption
  ) =

  interface IRouteLinks with
    member _.TryBuild(name, parameters) = table.TryBuildUrl(name, parameters)

    member _.IsActive(name, parameters) =
      match table.TryBuildUrl(name, parameters) with
      | Error _ -> false
      | Ok url ->
        current() |> ValueOption.exists(fun context -> context.Path = url)

type InMemoryHistory() =
  let entries = ResizeArray<HistoryEntry>()
  let mutable index = -1
  let mutable cachedSnapshot: HistorySnapshot option = None
  let changed = Event<HistoryChange>()

  let tryAt target =
    if target >= 0 && target < entries.Count then
      Some entries[target]
    else
      None

  let snapshot () =
    match cachedSnapshot with
    | Some value -> value
    | None ->
      let value = {
        Entries =
          (entries.ToArray() |> Array.AsReadOnly) :> IReadOnlyList<HistoryEntry>
        Index = index
        Current = tryAt index
      }

      cachedSnapshot <- Some value
      value

  let invalidate () = cachedSnapshot <- None

  let raiseChange kind entry previous =
    changed.Trigger {
      Kind = kind
      Entry = entry
      Previous = previous
    }

  interface IHistoryStore with
    member _.Snapshot = snapshot()
    member _.CanGoBack = index > 0

    member _.CanGoForward = index >= 0 && index < entries.Count - 1

    member _.Changed = changed.Publish

    member _.Current = tryAt index

    member _.Push(url, state) =
      let previous = tryAt index

      if entries.Count > index + 1 then
        entries.RemoveRange(index + 1, entries.Count - index - 1)

      let entry = {
        Key = Guid.NewGuid()
        Url = url
        State = state
        Index = entries.Count
      }

      entries.Add entry
      index <- entries.Count - 1
      invalidate()
      raiseChange HistoryChangeKind.Push entry previous
      entry

    member _.Replace(url, state) =
      let current = tryAt index

      match current with
      | None ->
        let entry = {
          Key = Guid.NewGuid()
          Url = url
          State = state
          Index = entries.Count
        }

        entries.Add entry
        index <- entries.Count - 1
        invalidate()
        raiseChange HistoryChangeKind.Replace entry current
        entry
      | Some _ ->
        let entry = {
          entries[index] with
              Url = url
              State = state
        }

        entries[index] <- entry
        invalidate()
        raiseChange HistoryChangeKind.Replace entry current
        entry

    member _.Peek(target: int) = tryAt target

    member _.Commit(target: int) =
      match tryAt target with
      | None -> None
      | Some _ ->
        let previous = tryAt index

        index <- target
        let entry = entries[index]
        invalidate()
        raiseChange HistoryChangeKind.Go entry previous
        Some entry

type RouterImpl<'View>(config: RouterConfig<'View>, table: RouteTable<'View>) =
  let gate = obj()
  let navigationSemaphore = new SemaphoreSlim(1, 1)
  let cache = ConcurrentDictionary<string, CacheEntry<'View>>()
  let changed = Event<RouterSnapshot<'View>>()
  let history = config.History
  let logger = config.Logger

  let mutable activeRoute: RouteContext voption = ValueNone
  let mutable activeDefinition: RouteDefinition<'View> voption = ValueNone
  let mutable content: 'View voption = config.Splash |> ValueOption.ofOption
  let mutable state = Idle
  let mutable activeNavigation: CancellationTokenSource option = None
  let mutable cachedSnapshot: RouterSnapshot<'View> option = None

  let invalidateSnapshot () =
    lock gate (fun () -> cachedSnapshot <- None)

  do history.Changed.Add(fun _ -> invalidateSnapshot())

  let snapshot () =
    lock
      gate
      (fun () ->
        match cachedSnapshot with
        | Some value -> value
        | None ->
          let value = {
            State = state
            Route = activeRoute
            Content = content
            History = history.Snapshot
          }

          cachedSnapshot <- Some value
          value
      )

  let publish () =
    invalidateSnapshot()
    let value = snapshot()
    changed.Trigger value

  let logError (ex: exn) (message: string) =
    logger |> Option.iter(fun log -> log.LogError(ex, message))

  let globalGuards = config.Guards |> List.map AsyncGuardWork

  let checkCancellation (token: CancellationToken) =
    if token.IsCancellationRequested then
      raise(OperationCanceledException(token))

  let resolveTarget
    (target: NavigationTarget)
    : Result<CompiledRoute<'View> * RouteContext, NavigationError> =
    match target with
    | Url url -> table.Match(url, ?logger = logger)
    | Route(name, parameters) ->
      table.TryBuildUrl(name, parameters)
      |> Result.mapError InvalidTarget
      |> Result.bind(fun url -> table.Match(url, ?logger = logger))

  [<TailCall>]
  let rec resolveRedirectTarget
    (target: NavigationTarget)
    (chain: string list)
    (redirectCount: int)
    : Result<RedirectResolution<'View>, NavigationError> =
    match resolveTarget target with
    | Error error -> Error error
    | Ok(route, context) ->
      match route.Definition.redirect with
      | None ->
        Ok {
          Route = route
          Context = context
          Redirects = chain
          Count = redirectCount
        }
      | Some next ->
        if redirectCount >= config.MaxRedirects then
          Error(RedirectLoop(List.rev(context.Path :: chain)))
        else
          resolveRedirectTarget next (context.Path :: chain) (redirectCount + 1)

  let navigateOnce
    (navigable: INavigable)
    (target: NavigationTarget)
    (redirects: string list)
    : CancellableValueTask<Result<ActivatedNavigation<'View>, NavigationError>> =
    cancellableValueTask {
      let! token = CancellableValueTask.getCancellationToken()

      let mutable currentTarget = target
      let mutable currentRedirects = redirects
      let mutable redirectCount = List.length redirects
      let mutable outcome = ValueNone

      while outcome.IsNone do
        checkCancellation token

        match
          resolveRedirectTarget currentTarget currentRedirects redirectCount
        with
        | Error error -> outcome <- ValueSome(Error error)
        | Ok resolution ->
          currentRedirects <- resolution.Redirects
          redirectCount <- resolution.Count

          match!
            Guards.runStages
              globalGuards
              activeRoute
              activeDefinition
              resolution.Route
              resolution.Context
          with
          | Halted error -> outcome <- ValueSome(Error error)
          | RedirectTo url ->
            match
              Redirects.tryFollow
                config.MaxRedirects
                url
                currentRedirects
                redirectCount
            with
            | Error error -> outcome <- ValueSome(Error error)
            | Ok(nextTarget, nextRedirects, nextCount) ->
              currentTarget <- nextTarget
              currentRedirects <- nextRedirects
              redirectCount <- nextCount
          | Allowed ->
            match
              ViewCache.tryGet
                cache
                resolution.Route.Definition.cache
                resolution.Context
            with
            | ValueSome view ->
              outcome <-
                ValueSome(
                  Ok {
                    Route = resolution.Route
                    Context = resolution.Context
                    View = view
                    Redirects = currentRedirects
                  }
                )
            | ValueNone ->
              let! view =
                Views.run
                  resolution.Route.Definition
                  resolution.Context
                  navigable

              ViewCache.store
                (fun (active: RouteContext) ->
                  ViewCache.isActive activeRoute active
                )
                cache
                resolution.Route.Definition.cache
                resolution.Context
                view

              outcome <-
                ValueSome(
                  Ok {
                    Route = resolution.Route
                    Context = resolution.Context
                    View = view
                    Redirects = currentRedirects
                  }
                )

      match outcome with
      | ValueSome result -> return result
      | ValueNone -> return Error Cancelled
    }

  let toCancellableTask
    (cancelled: 'T)
    (work: CancellableValueTask<'T>)
    : CancellableTask<'T> =
    fun token ->
      if token.IsCancellationRequested then
        Task.FromResult cancelled
      else
        (work token).AsTask()

  member this.NavigateCore
    (target: NavigationTarget, historyIndex: int option)
    : CancellableValueTask<Result<NavigationResult, NavigationError>> =
    cancellableValueTask {
      let! token = CancellableValueTask.getCancellationToken()
      let linked = CancellationTokenSource.CreateLinkedTokenSource(token)

      lock
        gate
        (fun () ->
          match activeNavigation with
          | Some previous ->
            previous.Cancel()
            previous.Dispose()
          | None -> ()

          activeNavigation <- Some linked
          state <- Navigating
        )

      let clearNavigation () =
        lock
          gate
          (fun () ->
            match activeNavigation with
            | Some current when obj.ReferenceEquals(current, linked) ->
              state <- Idle
              activeNavigation <- None
            | _ -> ()
          )

        linked.Dispose()
        publish()

      publish()

      try
        match! navigateOnce (this :> INavigable) target [] linked.Token with
        | Error error ->
          clearNavigation()
          return Error error
        | Ok {
               Route = route
               Context = context
               View = view
               Redirects = redirects
             } ->
          let previousRoute = activeRoute
          let previousDefinition = activeDefinition

          let accepted =
            lock
              gate
              (fun () ->
                match activeNavigation with
                | Some current when obj.ReferenceEquals(current, linked) ->
                  activeDefinition <- ValueSome route.Definition
                  activeRoute <- ValueSome context
                  content <- ValueSome view
                  state <- Idle
                  activeNavigation <- None
                  true
                | _ -> false
              )

          if not accepted then
            linked.Dispose()
            publish()
            return Error Superseded
          else
            let entry =
              HistoryNavigation.resolveEntry
                history
                config.SameUrl
                historyIndex
                context
                redirects

            RouteLifetime.disposeIfUncached previousDefinition previousRoute

            linked.Dispose()
            publish()

            return
              Ok {
                Route = context
                History = entry
                Redirects = redirects
              }
      with
      | :? OperationCanceledException ->
        clearNavigation()
        return Error Cancelled
      | error ->
        logError error "Navigation failed"
        clearNavigation()
        return Error(ActivationFailed("navigation", Guid.NewGuid()))
    }

  member this.NavigateWithPolicy
    (target: NavigationTarget, historyIndex: int option)
    : CancellableValueTask<Result<NavigationResult, NavigationError>> =
    cancellableValueTask {
      let! token = CancellableValueTask.getCancellationToken()

      match config.Concurrency with
      | Serialize ->
        try
          do! navigationSemaphore.WaitAsync(token)

          try
            return! this.NavigateCore(target, historyIndex)
          finally
            navigationSemaphore.Release() |> ignore
        with :? OperationCanceledException ->
          return Error Cancelled
      | Reject ->
        let busy = lock gate (fun () -> activeNavigation.IsSome)

        if busy then
          return Error Superseded
        else
          return! this.NavigateCore(target, historyIndex)
      | LatestWins -> return! this.NavigateCore(target, historyIndex)
    }

  member this.Navigate
    (url: string)
    : CancellableTask<Result<NavigationResult, NavigationError>> =
    toCancellableTask (Error Cancelled) (this.NavigateWithPolicy(Url url, None))

  member this.NavigateByName
    (name: string, parameters: RouteParams)
    : CancellableTask<Result<NavigationResult, NavigationError>> =
    toCancellableTask
      (Error Cancelled)
      (this.NavigateWithPolicy(NavigationTarget.Route(name, parameters), None))

  member this.Back
    ()
    : CancellableTask<Result<NavigationResult, NavigationError>> =
    toCancellableTask
      (Error Cancelled)
      (cancellableValueTask {
        match history.Snapshot.Index with
        | index when index > 0 ->
          match history.Peek(index - 1) with
          | Some entry ->
            return! this.NavigateWithPolicy(Url entry.Url, Some(index - 1))
          | None ->
            return Error(HistoryFailed "Previous history entry was not found")
        | _ -> return Error(HistoryFailed "There is no previous history entry")
      })

  member this.Forward
    ()
    : CancellableTask<Result<NavigationResult, NavigationError>> =
    toCancellableTask
      (Error Cancelled)
      (cancellableValueTask {
        let snapshot = history.Snapshot

        if snapshot.CanGoForward then
          match history.Peek(snapshot.Index + 1) with
          | Some entry ->
            return!
              this.NavigateWithPolicy(Url entry.Url, Some(snapshot.Index + 1))
          | None ->
            return Error(HistoryFailed "Next history entry was not found")
        else
          return Error(HistoryFailed "There is no next history entry")
      })

  interface INavigable with
    member this.Navigate(url) = this.Navigate url

    member this.NavigateByName(name, parameters) =
      this.NavigateByName(name, parameters)

    member this.Back() = this.Back()
    member this.Forward() = this.Forward()

  interface IRouter<'View> with
    member _.Snapshot = snapshot()
    member _.Changed = changed.Publish
    member _.History = history :> IHistory

    member _.ClearCache() =
      for pair in cache do
        pair.Value.Context.Disposables.Dispose()

      cache.Clear()

    member _.Links =
      RouteLinks(table, fun () -> lock gate (fun () -> activeRoute))
      :> IRouteLinks

[<Sealed; Class>]
type Router =

  static member create<'View>(config: RouterConfig<'View>) =
    RouteTable.Compile(config.Routes)
    |> Result.map(fun table ->
      RouterImpl<'View>(config, table) :> IRouter<'View>
    )

  [<CompiledName "CreateDefault">]
  static member createDefault<'View>(routes: RouteDefinition<'View> seq) =
    let config = {
      Routes = List.ofSeq routes
      History = InMemoryHistory()
      Splash = None
      Guards = []
      Cache = NoCache
      Concurrency = LatestWins
      SameUrl = Ignore
      MaxRedirects = 5
      Logger = None
    }

    Router.create config
