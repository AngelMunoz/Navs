namespace Navs.Router

open System
open System.Collections.Generic
open System.Runtime.InteropServices

open FsToolkit.ErrorHandling

open FSharp.Data.Adaptive

open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser
open UrlTemplates.UrlTemplate

open Navs
open System.Threading.Tasks

[<Struct; NoComparison>]
type ActiveRouteParams = {
  SegmentIndex: int
  ParamName: string
  ParamValue: string
}

[<NoComparison; NoEquality>]
type RoutingEnv<'View> = {
  routes: RouteTrack<'View> seq
  state: cval<NavigationState>
  viewCache: cmap<string, ActiveRouteParams list * UrlInfo * 'View>
  activeRoute:
    cval<
      voption<(RouteContext * (RouteGuard<'View> * RouteDefinition<'View>) list)>
     >
  content: cval<voption<'View>>
}

[<NoComparison; NoEquality>]
type ParamResoluton<'View> = {
  nextRouteNodes: RouteTrack<'View> seq
  nextRouteParams: ActiveRouteParams list
  nextContext: RouteContext
  routeHit: RouteTrack<'View>
}

[<NoEquality; NoComparison>]
type RouteResolution<'View> = {
  view: 'View
  canDeactivateGuards: (RouteGuard<'View> * RouteDefinition<'View>) list
}

[<Struct; NoComparison>]
type Guards<'View> = {
  canActivate: list<RouteGuard<'View> * RouteDefinition<'View>>
  canDeactivate: list<RouteGuard<'View> * RouteDefinition<'View>>
}

[<Struct; NoComparison>]
type Redirection = { from: string; target: string }

module Dictionary =

  let areEqual (a: IDictionary<_, _>) (b: IDictionary<_, _>) =
    if a = b then
      true
    else if a.Count <> b.Count then
      false
    else
      a
      |> Seq.forall(fun (KeyValue(k, v)) ->
        match b.TryGetValue k with
        | true, v' -> v = v'
        | _ -> false
      )



module RouteTracks =

  let rec internal processChildren pattern parent children =
    match children with
    | [] -> []
    | child :: rest ->
      let childTrack = {
        pathPattern = $"{pattern}/{child.pattern}"
        routeDefinition = child
        parentTrack = parent
        children = []
      }

      {
        childTrack with
            children =
              processChildren
                $"{pattern}/{child.pattern}"
                (ValueSome childTrack)
                child.children
      }
      :: processChildren pattern parent rest

  let internal getDefinition
    currentPattern
    (parent: RouteTrack<'View> voption)
    (track: RouteDefinition<'View>)
    =
    let queue =
      Queue<
        string *
        RouteTrack<'View> voption *
        RouteDefinition<'View> *
        RouteTrack<'View> list
       >()

    let result = ResizeArray<RouteTrack<'View>>()

    queue.Enqueue(currentPattern, parent, track, [])

    while queue.Count > 0 do
      let currentPattern, parent, track, siblings = queue.Dequeue()

      let pattern =
        if currentPattern = "" then
          track.pattern
        else if parent.IsSome && currentPattern.EndsWith('/') then
          $"{currentPattern}{track.pattern}"
        else
          $"{currentPattern}/{track.pattern}"

      let currentTrack = {
        pathPattern = pattern
        routeDefinition = track
        parentTrack = parent
        children = siblings
      }

      result.Add currentTrack

      let childrenTracks =
        processChildren pattern (ValueSome currentTrack) track.children

      for childTrack in childrenTracks do
        queue.Enqueue(
          pattern,
          ValueSome currentTrack,
          childTrack.routeDefinition,
          childTrack.children
        )

    result

  [<CompiledName "FromDefinitions">]
  let fromDefinitions (routes: RouteDefinition<'View> seq) = [
    for route in routes do
      yield! getDefinition "" ValueNone route
  ]

module RoutingEnv =

  let get<'View>
    (routes: RouteDefinition<'View> seq, splash: (unit -> 'View) option)
    =
    let routes = RouteTracks.fromDefinitions routes

    {
      routes = routes
      state = cval Idle
      viewCache = cmap()
      activeRoute = cval(ValueNone)
      content =
        cval(splash |> ValueOption.ofOption |> ValueOption.map(fun f -> f()))
    }

module Result =
  let inline requireValueSome (msg: string) (value: 'a voption) =
    match value with
    | ValueSome a -> Ok a
    | ValueNone -> Error msg

module RouteInfo =

  let getParamDiff (urlInfo: UrlInfo) (tplInfo: UrlTemplate) =
    if urlInfo.Segments.Length <> tplInfo.Segments.Length then
      []
    else
      urlInfo.Segments
      |> List.zip tplInfo.Segments
      |> List.indexed
      |> List.choose(fun (index, (segment, urlSegment)) ->
        match segment with
        | ParamSegment(name, _) ->
          Some(
            {
              SegmentIndex = index
              ParamName = name
              ParamValue = urlSegment
            }
          )
        | _ -> None
      )

  let digUpToRoot (track: RouteTrack<'View>) =
    let queue = Queue<RouteTrack<'View>>()
    let result = ResizeArray<RouteTrack<'View>>()

    queue.Enqueue(track)

    while queue.Count > 0 do
      let currentTrack = queue.Dequeue()
      result.Add currentTrack

      match currentTrack.parentTrack with
      | ValueSome parent -> queue.Enqueue(parent)
      | ValueNone -> ()

    result


  let getActiveRouteInfo (routes: RouteTrack<'View> seq) (url: string) = result {
    let! activeGraph, routeContext =
      voption {
        let! track, matchInfo =
          routes
          |> Seq.tryPick(fun route ->
            match RouteMatcher.matchStrings route.pathPattern url with
            | Ok(template, urlinfo, matchInfo) ->
              Some(
                route,
                {|
                  Route = url
                  UrlInfo = urlinfo
                  UrlMatch = matchInfo
                  UrlTemplate = template
                |}
              )
            | Error whytho -> None
          )

        let tracks = digUpToRoot track
        return tracks, matchInfo
      }
      |> Result.requireValueSome "No matching route found"

    let urlParam = getParamDiff routeContext.UrlInfo routeContext.UrlTemplate

    return
      activeGraph,
      urlParam,
      {
        path = url
        urlInfo = routeContext.UrlInfo
        urlMatch = routeContext.UrlMatch
      }
  }

  let extractGuards (activeGraph: RouteTrack<'View> seq) =

    let canActivate = Stack<RouteGuard<'View> * RouteDefinition<'View>>()
    let canDeactivate = Queue<RouteGuard<'View> * RouteDefinition<'View>>()

    activeGraph
    |> Seq.iter(fun route ->
      for guard in route.routeDefinition.canActivate do
        canActivate.Push(guard, route.routeDefinition)

      for guard in route.routeDefinition.canDeactivate do
        canDeactivate.Enqueue(guard, route.routeDefinition)
    )

    {
      canActivate = [
        while canActivate.Count > 0 do
          canActivate.Pop()
      ]
      canDeactivate = [
        while canDeactivate.Count > 0 do
          canDeactivate.Dequeue()
      ]
    }

module Navigable =

  let runGuards<'View>
    (navigable: INavigable<'View>)
    (onStop: string -> NavigationError<'View>)
    (guards: (RouteGuard<'View> * RouteDefinition<'View>) list)
    nextContext
    =
    async {
      let! token = Async.CancellationToken

      return!
        guards
        |> List.traverseAsyncResultM(fun (guard, definition) -> asyncResult {
          if token.IsCancellationRequested then
            return! Error NavigationCancelled
          else
            let! result = guard nextContext navigable token |> Async.AwaitTask

            match result with
            | Continue -> return ()
            | Stop -> return! Error(onStop definition.name)
            | Redirect url -> return! Error(GuardRedirect url)

        })
        |> AsyncResult.ignore
    }

  let canDeactivate (nav: ref<INavigable<_>>) activeContext = async {
    match activeContext |> AVal.force with
    | ValueNone -> return Ok()
    | ValueSome(activeContext, activeGuards) ->

      let! result =
        runGuards nav.Value CantDeactivate activeGuards activeContext

      match result with
      | Error(GuardRedirect _) ->
        return Error(CantDeactivate activeContext.path)
      | value -> return value
  }

  let canActivate (nav: ref<INavigable<_>>) (nextContext, activeRouteNodes) = asyncResult {
    let guards = RouteInfo.extractGuards activeRouteNodes

    do! runGuards nav.Value CantActivate guards.canActivate nextContext

    return guards
  }

  let tryGetFromCache
    (liveNodes: cmap<string, ActiveRouteParams list * UrlInfo * 'View>)
    =
    fun (key, nextRouteParams, nextUrlInfo: UrlInfo) ->
      match liveNodes.TryGetValue key with
      | Some(oldParams, oldUrlInfo, oldView) ->
        if nextRouteParams = oldParams then
          if Dictionary.areEqual oldUrlInfo.Query nextUrlInfo.Query then
            ValueSome oldView
          else
            ValueNone
        else
          ValueNone
      | None -> ValueNone

  let resolveView tryGetFromCache (navigable: ref<INavigable<_>>) =
    fun paramResolution -> asyncResult {

      let renderView =
        fun ctx -> asyncResult {
          let! token = Async.CancellationToken

          do!
            token.IsCancellationRequested
            |> Result.requireFalse NavigationCancelled

          let! result =
            paramResolution.routeHit.routeDefinition.getContent
              ctx
              navigable.Value
              token
            |> Async.AwaitTask
            |> Async.Catch

          match result with
          | Choice1Of2 view -> return view
          | Choice2Of2 ex -> return! Error NavigationCancelled
        }

      // 5. Will Render view
      match paramResolution.routeHit.routeDefinition.cacheStrategy with
      | NoCache ->
        let! view = renderView paramResolution.nextContext

        return view
      | Cache ->
        match
          tryGetFromCache(
            paramResolution.routeHit.pathPattern,
            paramResolution.nextRouteParams,
            paramResolution.nextContext.urlInfo
          )
        with
        | ValueSome view -> return view
        | ValueNone ->
          let! view = renderView paramResolution.nextContext

          return view
    }

  let resolveParams routingEnv url = asyncResult {

    let! nextRouteNodes, nextRouteParams, nextContext =
      RouteInfo.getActiveRouteInfo routingEnv.routes url
      |> Result.mapError(fun _ -> RouteNotFound url)

    let! routeHit =
      nextRouteNodes
      // The first route is the currently active route
      // The rest of the routes are the matched "parent" routes.
      |> Seq.tryHead
      |> Result.requireSome(RouteNotFound url)

    return {
      nextRouteNodes = nextRouteNodes
      nextRouteParams = nextRouteParams
      nextContext = nextContext
      routeHit = routeHit
    }
  }

  type private CanDeactivateExecutor<'View> =
    (voption<RouteContext * list<RouteGuard<'View> * RouteDefinition<'View>>>) cval
      -> Async<Result<unit, NavigationError<'View>>>

  type private CanActivateExecutor<'View> =
    RouteContext * seq<RouteTrack<'View>>
      -> Async<Result<Guards<'View>, NavigationError<'View>>>

  type private ResolveViewExecutor<'View> =
    ParamResoluton<'View> -> Async<Result<'View, NavigationError<'View>>>

  let navigateByUrl
    (
      routingEnv,
      resolveParams,
      resolveView: ResolveViewExecutor<'View>,
      canActivate: CanActivateExecutor<'View>,
      canDeactivate: CanDeactivateExecutor<'View>
    ) =
    fun (url: string) -> asyncResult {
      let! token = Async.CancellationToken

      // 1. Can Deactivate
      do! canDeactivate routingEnv.activeRoute

      // Navigation cancelled is not part of lifecycle
      do!
        token.IsCancellationRequested |> Result.requireFalse NavigationCancelled

      // 2. Resolve URl and Parameters
      let! resolveParams =
        resolveParams url
        |> AsyncResult.teeError(fun error ->
          transact(fun _ -> routingEnv.activeRoute.Value <- ValueNone)
        )

      // 3. Can Activate
      let! guards =
        canActivate(resolveParams.nextContext, resolveParams.nextRouteNodes)

      // 4. Render View
      let! rendered = resolveView resolveParams

      match resolveParams.routeHit.routeDefinition.cacheStrategy with
      | NoCache ->
        transact(fun _ ->
          routingEnv.activeRoute.Value <-
            ValueSome(resolveParams.nextContext, guards.canDeactivate)

          routingEnv.content.Value <- ValueSome rendered
        )
      | Cache ->
        transact(fun _ ->
          routingEnv.viewCache.Add(
            resolveParams.routeHit.pathPattern,
            (resolveParams.nextRouteParams,
             resolveParams.nextContext.urlInfo,
             rendered)
          )
          |> ignore

          routingEnv.activeRoute.Value <-
            ValueSome(resolveParams.nextContext, guards.canDeactivate)

          routingEnv.content.Value <- ValueSome rendered
        )
    }

  let navigateByName routingEnv nav redirectionStack =
    fun name routeParams -> asyncResult {
      let tryGetFromCache = tryGetFromCache routingEnv.viewCache
      let resolveParams = resolveParams routingEnv
      let resolveView = resolveView tryGetFromCache nav
      let canActivate = canActivate nav
      let canDeactivate = canDeactivate nav

      let navigateByUrl =
        navigateByUrl(
          routingEnv,
          resolveParams,
          resolveView,
          canActivate,
          canDeactivate
        )

      let routeParams: IReadOnlyDictionary<string, obj> =
        routeParams
        // Guess what! double check for NRTs that may come from dotnet langs/types
        |> Option.map(fun p -> p |> Option.ofNull)
        |> Option.flatten
        |> Option.defaultWith(fun _ -> Dictionary())

      let! route =
        routingEnv.routes
        |> Seq.tryFind(fun route -> route.routeDefinition.name = name)
        |> Result.requireSome(RouteNotFound name)

      let! url =
        UrlTemplate.toUrl route.pathPattern routeParams
        |> Result.mapError(fun e -> RouteNotFound(String.concat ", " e))

      return! navigateByUrl url
    }

  let get<'View> routingEnv =
    let navigable = ref Unchecked.defaultof<INavigable<_>>
    let tryGetFromCache = tryGetFromCache routingEnv.viewCache
    let resolveParams = resolveParams routingEnv
    let resolveView = resolveView tryGetFromCache navigable
    let canActivate = canActivate navigable
    let canDeactivate = canDeactivate navigable

    let navigateByUrl =
      navigateByUrl(
        routingEnv,
        resolveParams,
        resolveView,
        canActivate,
        canDeactivate
      )

    navigable.Value <-
      { new INavigable<'View> with

          override _.State = routingEnv.state

          override _.StateSnapshot = routingEnv.state |> AVal.force

          override _.Navigate(url, ?cancellationToken) = task {
            let redirectionStack = Stack<Redirection>()

            try
              transact(fun _ -> routingEnv.state.Value <- Navigating)

              try
                let! result =
                  Async.StartImmediateAsTask(
                    navigateByUrl url,
                    ?cancellationToken = cancellationToken
                  )

                let mutable lastResult = result

                match result with
                | Ok _ -> ()
                | Error(GuardRedirect redirectTo) ->
                  redirectionStack.Push({ from = url; target = redirectTo })
                | Error _ -> redirectionStack.Clear()

                while redirectionStack.Count > 0 do
                  let { from = from; target = target } = redirectionStack.Pop()

                  let! result =
                    Async.StartImmediateAsTask(
                      navigateByUrl target,
                      ?cancellationToken = cancellationToken
                    )

                  lastResult <- result

                  match result with
                  | Ok _ -> ()
                  | Error(GuardRedirect redirectTo) ->

                    if target = redirectTo then
                      ()
                    else
                      redirectionStack.Push(
                        { from = from; target = redirectTo }
                      )
                  | Error _ -> redirectionStack.Clear()

                return lastResult

              with
              | :? TaskCanceledException
              | :? OperationCanceledException ->
                transact(fun _ -> routingEnv.activeRoute.Value <- ValueNone)

                return Error NavigationCancelled
              | ex ->
                transact(fun _ -> routingEnv.activeRoute.Value <- ValueNone)

                return Error(NavigationFailed ex.Message)

            finally
              transact(fun _ -> routingEnv.state.Value <- Idle)
          }

          override _.NavigateByName(name, ?routeParams, ?cancellationToken) = task {
            let redirectionStack = Stack<Redirection>()

            try
              transact(fun _ -> routingEnv.state.Value <- Navigating)

              try
                let! result =
                  Async.StartImmediateAsTask(
                    navigateByName
                      routingEnv
                      navigable
                      redirectionStack
                      name
                      routeParams,
                    ?cancellationToken = cancellationToken
                  )

                let mutable lastResult = result

                match result with
                | Ok _ -> ()
                | Error(GuardRedirect redirectTo) ->
                  printfn "Redirecting to %s" redirectTo
                  redirectionStack.Push({ from = name; target = redirectTo })
                | Error _ -> redirectionStack.Clear()

                while redirectionStack.Count > 0 do
                  let { from = from; target = target } = redirectionStack.Pop()

                  let! result =
                    Async.StartImmediateAsTask(
                      navigateByUrl target,
                      ?cancellationToken = cancellationToken
                    )

                  lastResult <- result

                  match result with
                  | Ok _ -> ()
                  | Error(GuardRedirect redirectTo) ->
                    printfn "Redirecting to %s" redirectTo

                    if from = from && target = redirectTo then
                      ()
                    else
                      redirectionStack.Push(
                        { from = from; target = redirectTo }
                      )
                  | Error _ -> redirectionStack.Clear()

                return lastResult
              with
              | :? TaskCanceledException
              | :? OperationCanceledException ->
                transact(fun _ -> routingEnv.activeRoute.Value <- ValueNone)

                return Error NavigationCancelled
              | ex ->
                transact(fun _ -> routingEnv.activeRoute.Value <- ValueNone)

                return Error(NavigationFailed ex.Message)
            finally
              transact(fun _ -> routingEnv.state.Value <- Idle)
          }
      }

    navigable.Value

[<Sealed; Class>]
type Router =

  static member get<'View>(env: RoutingEnv<'View>, nav: INavigable<'View>) =
    { new IRouter<'View> with

        member _.State = nav.State

        member _.StateSnapshot = nav.StateSnapshot

        member _.Route =
          env.activeRoute
          |> AVal.map(
            function
            | ValueSome v -> ValueSome(fst v)
            | ValueNone -> ValueNone
          )

        member _.RouteSnapshot =
          env.activeRoute
          |> AVal.map(
            function
            | ValueSome v -> ValueSome(fst v)
            | ValueNone -> ValueNone
          )
          |> AVal.force

        member _.ContentSnapshot = env.content |> AVal.force

        member _.Content = env.content

        member _.Navigate(url, ?cancellationToken) =
          nav.Navigate(url, ?cancellationToken = cancellationToken)

        member _.NavigateByName(name, ?routeParams, ?cancellationToken) =
          nav.NavigateByName(
            name,
            ?routeParams = routeParams,
            ?cancellationToken = cancellationToken
          )
    }

  /// <summary>
  /// Creates a new router with the provided routes.
  /// </summary>
  /// <param name="routes">The routes that the router will use to match the URL and render the view</param>
  /// <param name="splash">
  /// The router initially doesn't have a view to render. You can provide this function
  /// to supply a splash-like (like mobile devices initial screen) view to render while you trigger the first navigation.
  /// </param>
  [<CompiledName "Get">]
  static member get<'View>(routes, [<Optional>] ?splash) =
    let env = RoutingEnv.get<'View>(routes, splash)

    let navigable = Navigable.get env


    { new IRouter<'View> with

        member _.State = navigable.State

        member _.StateSnapshot = navigable.StateSnapshot

        member _.Route =
          env.activeRoute
          |> AVal.map(
            function
            | ValueSome(v, _) -> ValueSome v
            | ValueNone -> ValueNone
          )

        member _.RouteSnapshot =
          env.activeRoute
          |> AVal.map(
            function
            | ValueSome(v, _) -> ValueSome v
            | ValueNone -> ValueNone
          )
          |> AVal.force

        member _.ContentSnapshot = env.content |> AVal.force

        member _.Content = env.content

        member _.Navigate(url, ?cancellationToken) =
          navigable.Navigate(url, ?cancellationToken = cancellationToken)

        member _.NavigateByName(name, ?routeParams, ?cancellationToken) =
          navigable.NavigateByName(
            name,
            ?routeParams = routeParams,
            ?cancellationToken = cancellationToken
          )
    }
