namespace Navs

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

open IcedTasks
open Microsoft.Extensions.Logging
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser

[<Interface>]
type IDisposableBag =
  inherit IDisposable
  abstract AddDisposable: IDisposable -> unit

[<NoComparison; NoEquality>]
type RouteContext = {
  Path: string
  UrlInfo: UrlInfo
  UrlMatch: UrlMatch
  Disposables: IDisposableBag
} with

  member this.GetParam<'T>(name: string) =
    UrlMatch.getFromParams<'T> name this.UrlMatch

  member this.GetParamSequence<'T>(name: string) =
    UrlMatch.getParamSeqFromQuery<'T> name this.UrlMatch
    |> ValueOption.defaultValue Seq.empty

  member this.AddDisposable(disposable: IDisposable) =
    this.Disposables.AddDisposable disposable

[<Extension; Class; Sealed>]
type RouteContextExtensions =

  [<Extension; CompiledName "GetParam">]
  static member inline GetParam<'T>(context: RouteContext, name: string) =
    context.GetParam<'T>(name)

  [<Extension; CompiledName "GetParamSequence">]
  static member inline GetParamSequence<'T>
    (context: RouteContext, name: string)
    =
    context.GetParamSequence<'T>(name)

  [<Extension; CompiledName "AddDisposable">]
  static member inline AddDisposable
    (context: RouteContext, disposable: IDisposable)
    =
    context.AddDisposable(disposable)

type RouteParams = IReadOnlyDictionary<string, obj>

[<Struct; NoComparison>]
type NavigationState =
  | Idle
  | Navigating

type NavigationError =
  | InvalidTarget of target: string
  | RouteNotFound of url: string
  | GuardStopped of route: string
  | GuardFailed of route: string * correlationId: Guid
  | RedirectLoop of chain: string list
  | ActivationFailed of stage: string * correlationId: Guid
  | HistoryFailed of message: string
  | Cancelled
  | Superseded

type HistoryEntry = {
  Key: Guid
  Url: string
  State: obj option
  Index: int
}

type HistorySnapshot = {
  Entries: System.Collections.Generic.IReadOnlyList<HistoryEntry>
  Index: int
  Current: HistoryEntry option
} with

  member this.CanGoBack = this.Index > 0

  member this.CanGoForward =
    this.Index >= 0 && this.Index < this.Entries.Count - 1

type HistoryChangeKind =
  | Push
  | Replace
  | Go

type HistoryChange = {
  Kind: HistoryChangeKind
  Entry: HistoryEntry
  Previous: HistoryEntry option
}

[<Interface>]
type IHistory =
  abstract Snapshot: HistorySnapshot
  abstract CanGoBack: bool
  abstract CanGoForward: bool
  abstract Changed: IEvent<HistoryChange>
  abstract Current: HistoryEntry option

[<Interface>]
type IHistoryStore =
  inherit IHistory
  abstract Push: url: string * ?state: obj -> HistoryEntry
  abstract Replace: url: string * ?state: obj -> HistoryEntry
  abstract Peek: index: int -> HistoryEntry option
  abstract Commit: index: int -> HistoryEntry option

type NavigationResult = {
  Route: RouteContext
  History: HistoryEntry
  Redirects: string list
}

type NavigationTarget =
  | Url of url: string
  | Route of name: string * parameters: RouteParams

[<Struct; NoComparison>]
type GuardResult =
  | Continue
  | Stop
  | Redirect of url: string
  | Cancel

type CachePolicy =
  | NoCache
  | Cache of maxEntries: int * ttl: TimeSpan option

type ConcurrencyPolicy =
  | LatestWins
  | Serialize
  | Reject

type SameUrlPolicy =
  | Ignore
  | Replace
  | Push

[<Interface>]
type INavigable =
  abstract Navigate:
    url: string -> CancellableTask<Result<NavigationResult, NavigationError>>

  abstract NavigateByName:
    name: string * parameters: RouteParams ->
      CancellableTask<Result<NavigationResult, NavigationError>>

  abstract Back:
    unit -> CancellableTask<Result<NavigationResult, NavigationError>>

  abstract Forward:
    unit -> CancellableTask<Result<NavigationResult, NavigationError>>

type internal ViewWork<'View> =
  | SyncViewWork of (RouteContext -> INavigable -> ValueTask<'View>)
  | AsyncViewWork of (RouteContext -> INavigable -> CancellableTask<'View>)

type internal GuardWork =
  | SyncGuardWork of
    (RouteContext voption -> RouteContext -> ValueTask<GuardResult>)
  | AsyncGuardWork of
    (RouteContext voption -> RouteContext -> CancellableTask<GuardResult>)

[<NoComparison; NoEquality>]
type RouteDefinition<'View> = internal {
  name: string
  pattern: string
  priority: int
  view: ViewWork<'View>
  canActivate: GuardWork list
  canDeactivate: GuardWork list
  cache: CachePolicy
  redirect: NavigationTarget option
  meta: Map<string, obj>
} with

  member this.Name = this.name
  member this.Pattern = this.pattern
  member this.Priority = this.priority
  member this.CachePolicy = this.cache
  member this.RedirectTarget = this.redirect
  member this.Metadata = this.meta

type SyncView<'View> = RouteContext -> INavigable -> 'View

type TaskView<'View> =
  RouteContext -> INavigable -> CancellationToken -> Task<'View>

type AsyncView<'View> = RouteContext -> INavigable -> Async<'View>

type SyncGuard = RouteContext voption -> RouteContext -> GuardResult

type TaskGuard =
  RouteContext voption -> RouteContext -> CancellationToken -> Task<GuardResult>

type AsyncGuard = RouteContext voption -> RouteContext -> Async<GuardResult>

module RouteFactory =

  let create (name: string) (pattern: string) (view: ViewWork<'View>) = {
    name = name
    pattern = pattern
    priority = 0
    view = view
    canActivate = []
    canDeactivate = []
    cache = NoCache
    redirect = None
    meta = Map.empty
  }

  let appendCanActivate (work: GuardWork) (definition: RouteDefinition<'View>) = {
    definition with
        canActivate = definition.canActivate @ [ work ]
  }

  let appendCanDeactivate
    (work: GuardWork)
    (definition: RouteDefinition<'View>)
    =
    {
      definition with
          canDeactivate = definition.canDeactivate @ [ work ]
    }

[<Class>]
type Route =

  static member define<'View>
    (name: string, path: string, handler: SyncView<'View>)
    : RouteDefinition<'View> =
    RouteFactory.create
      name
      path
      (SyncViewWork(fun context nav -> valueTask { return handler context nav }))

  static member define<'View>
    (name: string, path: string, handler: TaskView<'View>)
    : RouteDefinition<'View> =
    RouteFactory.create name path (AsyncViewWork handler)

  [<CompiledName "Define">]
  static member defineCSharp<'View>
    (name: string, path: string, handler: Func<RouteContext, INavigable, 'View>)
    : RouteDefinition<'View> =
    Route.define<'View>(name, path, FuncConvert.FromFunc handler)

  [<CompiledName "Define">]
  static member defineCSharp<'View>
    (
      name: string,
      path: string,
      handler: Func<RouteContext, INavigable, CancellationToken, Task<'View>>
    ) : RouteDefinition<'View> =
    Route.define<'View>(name, path, FuncConvert.FromFunc handler)

  [<CompiledName "CanActivateCSharp">]
  static member canActivateCSharp<'View>
    (guard: Func<RouteContext, GuardResult>, definition: RouteDefinition<'View>)
    : RouteDefinition<'View> =
    definition
    |> RouteFactory.appendCanActivate(
      SyncGuardWork(fun _ next -> valueTask { return guard.Invoke next })
    )

  [<CompiledName "CanDeactivateCSharp">]
  static member canDeactivateCSharp<'View>
    (guard: Func<RouteContext, GuardResult>, definition: RouteDefinition<'View>)
    : RouteDefinition<'View> =
    definition
    |> RouteFactory.appendCanDeactivate(
      SyncGuardWork(fun _ next -> valueTask { return guard.Invoke next })
    )

  static member define<'View>
    (name: string, path: string, handler: AsyncView<'View>)
    : RouteDefinition<'View> =
    RouteFactory.create
      name
      path
      (AsyncViewWork(fun context nav -> cancellableTask {
        return! handler context nav
      }))

module Route =

  let cache policy (definition: RouteDefinition<'View>) = {
    definition with
        cache = policy
  }

  let priority value (definition: RouteDefinition<'View>) = {
    definition with
        priority = value
  }

  let redirect target (definition: RouteDefinition<'View>) = {
    definition with
        redirect = Some target
  }

  let meta key value (definition: RouteDefinition<'View>) = {
    definition with
        meta = Map.add key value definition.meta
  }

  let canActivate (guard: SyncGuard) (definition: RouteDefinition<'View>) =
    definition
    |> RouteFactory.appendCanActivate(
      SyncGuardWork(fun active next -> valueTask { return guard active next })
    )

  let canActivateTask (guard: TaskGuard) (definition: RouteDefinition<'View>) =
    definition |> RouteFactory.appendCanActivate(AsyncGuardWork guard)

  let canActivateAsync
    (guard: AsyncGuard)
    (definition: RouteDefinition<'View>)
    =
    definition
    |> RouteFactory.appendCanActivate(
      AsyncGuardWork(fun active next -> cancellableTask {
        return! guard active next
      })
    )

  let canDeactivate (guard: SyncGuard) (definition: RouteDefinition<'View>) =
    definition
    |> RouteFactory.appendCanDeactivate(
      SyncGuardWork(fun active next -> valueTask { return guard active next })
    )

  let canDeactivateTask
    (guard: TaskGuard)
    (definition: RouteDefinition<'View>)
    =
    definition |> RouteFactory.appendCanDeactivate(AsyncGuardWork guard)

  let canDeactivateAsync
    (guard: AsyncGuard)
    (definition: RouteDefinition<'View>)
    =
    definition
    |> RouteFactory.appendCanDeactivate(
      AsyncGuardWork(fun active next -> cancellableTask {
        return! guard active next
      })
    )

[<Interface>]
type IRouteLinks =
  abstract TryBuild:
    name: string * parameters: RouteParams -> Result<string, string>

  abstract IsActive: name: string * parameters: RouteParams -> bool

type RouterSnapshot<'View> = {
  State: NavigationState
  Route: RouteContext voption
  Content: 'View voption
  History: HistorySnapshot
}

[<Interface>]
type IRouter<'View> =
  inherit INavigable
  abstract Snapshot: RouterSnapshot<'View>
  abstract Changed: IEvent<RouterSnapshot<'View>>
  abstract History: IHistory
  abstract Links: IRouteLinks
  abstract ClearCache: unit -> unit

type GlobalGuard =
  RouteContext voption -> RouteContext -> CancellableTask<GuardResult>

[<Interface>]
type IViewCache<'View> =
  abstract TryGet: key: string -> ('View * RouteContext) option
  abstract Set: key: string * context: RouteContext * view: 'View -> unit
  abstract Invalidate: key: string -> unit
  abstract Clear: unit -> unit

type RouterConfig<'View> = {
  Routes: RouteDefinition<'View> list
  History: IHistoryStore
  Splash: 'View option
  Guards: GlobalGuard list
  Cache: CachePolicy
  Concurrency: ConcurrencyPolicy
  SameUrl: SameUrlPolicy
  MaxRedirects: int
  Logger: ILogger option
}

type RouteTableError =
  | EmptyRoutes
  | EmptyRouteName
  | DuplicateRouteName of name: string
  | DuplicatePattern of pattern: string
  | AmbiguousPattern of first: string * second: string
  | InvalidPattern of pattern: string * message: string
