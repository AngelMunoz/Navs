namespace Navs

open System
open System.Runtime.CompilerServices
open IcedTasks

[<Interface>]
type IDisposableBag =
  inherit IDisposable

  abstract AddDisposable: IDisposable -> unit

[<NoComparison; NoEquality>]
type RouteContext =
  { Path: string
    UrlInfo: UrlTemplates.UrlParser.UrlInfo
    UrlMatch: UrlTemplates.RouteMatcher.UrlMatch
    Disposables: IDisposableBag }

  member AddDisposable: disposable: IDisposable -> unit

  member GetParam: name: string -> 'T voption

  member GetParamSequence: name: string -> 'T seq

[<Extension; Sealed; Class>]
type RouteContextExtensions =

  [<Extension; CompiledName("AddDisposable")>]
  static member inline AddDisposable: context: RouteContext * disposable: IDisposable -> unit

  [<Extension; CompiledName("GetParam")>]
  static member inline GetParam: context: RouteContext * name: string -> 'T voption

  [<Extension; CompiledName("GetParamSequence")>]
  static member inline GetParamSequence: context: RouteContext * name: string -> 'T seq

type RouteParams = Collections.Generic.IReadOnlyDictionary<string, obj>

[<NoComparison; Struct>]
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

type HistoryEntry =
  { Key: Guid
    Url: string
    State: obj option
    Index: int }

type HistorySnapshot =
  { Entries: System.Collections.Generic.IReadOnlyList<HistoryEntry>
    Index: int
    Current: HistoryEntry option }

  member CanGoBack: bool

  member CanGoForward: bool

type HistoryChangeKind =
  | Push
  | Replace
  | Go

type HistoryChange =
  { Kind: HistoryChangeKind
    Entry: HistoryEntry
    Previous: HistoryEntry option }

[<Interface>]
type IHistory =

  abstract CanGoBack: bool

  abstract CanGoForward: bool

  abstract Changed: IEvent<HistoryChange>

  abstract Current: HistoryEntry option

  abstract Snapshot: HistorySnapshot

[<Interface>]
type IHistoryStore =
  inherit IHistory

  abstract Commit: index: int -> HistoryEntry option

  abstract Peek: index: int -> HistoryEntry option

  abstract Push: url: string * ?state: obj -> HistoryEntry

  abstract Replace: url: string * ?state: obj -> HistoryEntry

type NavigationResult =
  { Route: RouteContext
    History: HistoryEntry
    Redirects: string list }

type NavigationTarget =
  | Url of url: string
  | Route of name: string * parameters: RouteParams

[<NoComparison; Struct>]
type GuardResult =
  | Continue
  | Stop
  | Redirect of url: string
  | Cancel

type CachePolicy =
  | NoCache
  | Cache of maxEntries: int * ttl: System.TimeSpan option

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

  abstract Back: unit -> CancellableTask<Result<NavigationResult, NavigationError>>

  abstract Forward: unit -> CancellableTask<Result<NavigationResult, NavigationError>>

  abstract Navigate: url: string -> CancellableTask<Result<NavigationResult, NavigationError>>

  abstract NavigateByName:
    name: string * parameters: RouteParams -> CancellableTask<Result<NavigationResult, NavigationError>>

type internal ViewWork<'View> =
  | SyncViewWork of (RouteContext -> INavigable -> System.Threading.Tasks.ValueTask<'View>)
  | AsyncViewWork of
    (RouteContext -> INavigable -> System.Threading.CancellationToken -> System.Threading.Tasks.Task<'View>)

type internal GuardWork =
  | SyncGuardWork of (RouteContext voption -> RouteContext -> System.Threading.Tasks.ValueTask<GuardResult>)
  | AsyncGuardWork of
    (RouteContext voption
      -> RouteContext
      -> System.Threading.CancellationToken
      -> System.Threading.Tasks.Task<GuardResult>)

[<NoComparison; NoEquality>]
type RouteDefinition<'View> =
  internal
    { name: string
      pattern: string
      priority: int
      view: ViewWork<'View>
      canActivate: GuardWork list
      canDeactivate: GuardWork list
      cache: CachePolicy
      redirect: NavigationTarget option
      meta: Map<string, obj> }

  member CachePolicy: CachePolicy

  member Metadata: Map<string, obj>

  member Name: string

  member Pattern: string

  member Priority: int

  member RedirectTarget: NavigationTarget option

type SyncView<'View> = RouteContext -> INavigable -> 'View

type TaskView<'View> =
  RouteContext -> INavigable -> System.Threading.CancellationToken -> System.Threading.Tasks.Task<'View>

type AsyncView<'View> = RouteContext -> INavigable -> Async<'View>

type SyncGuard = RouteContext voption -> RouteContext -> GuardResult

type TaskGuard =
  RouteContext voption -> RouteContext -> System.Threading.CancellationToken -> System.Threading.Tasks.Task<GuardResult>

type AsyncGuard = RouteContext voption -> RouteContext -> Async<GuardResult>

[<Class>]
type Route =

  static member define: name: string * path: string * handler: AsyncView<'View> -> RouteDefinition<'View>

  static member define: name: string * path: string * handler: TaskView<'View> -> RouteDefinition<'View>

  static member define: name: string * path: string * handler: SyncView<'View> -> RouteDefinition<'View>

  [<CompiledName("Define")>]
  static member defineCSharp:
    name: string *
    path: string *
    handler:
      System.Func<RouteContext, INavigable, System.Threading.CancellationToken, System.Threading.Tasks.Task<'View>> ->
      RouteDefinition<'View>

  [<CompiledName("Define")>]
  static member defineCSharp:
    name: string * path: string * handler: System.Func<RouteContext, INavigable, 'View> -> RouteDefinition<'View>

  [<CompiledName("CanActivateCSharp")>]
  static member canActivateCSharp:
    guard: System.Func<RouteContext, GuardResult> * definition: RouteDefinition<'View> -> RouteDefinition<'View>

  [<CompiledName("CanDeactivateCSharp")>]
  static member canDeactivateCSharp:
    guard: System.Func<RouteContext, GuardResult> * definition: RouteDefinition<'View> -> RouteDefinition<'View>

module Route =

  val cache: policy: CachePolicy -> definition: RouteDefinition<'View> -> RouteDefinition<'View>

  val priority: value: int -> definition: RouteDefinition<'View> -> RouteDefinition<'View>

  val redirect: target: NavigationTarget -> definition: RouteDefinition<'View> -> RouteDefinition<'View>

  val meta: key: string -> value: obj -> definition: RouteDefinition<'View> -> RouteDefinition<'View>

  val canActivate: guard: SyncGuard -> definition: RouteDefinition<'View> -> RouteDefinition<'View>

  val canActivateTask: guard: TaskGuard -> definition: RouteDefinition<'View> -> RouteDefinition<'View>

  val canActivateAsync: guard: AsyncGuard -> definition: RouteDefinition<'View> -> RouteDefinition<'View>

  val canDeactivate: guard: SyncGuard -> definition: RouteDefinition<'View> -> RouteDefinition<'View>

  val canDeactivateTask: guard: TaskGuard -> definition: RouteDefinition<'View> -> RouteDefinition<'View>

  val canDeactivateAsync: guard: AsyncGuard -> definition: RouteDefinition<'View> -> RouteDefinition<'View>

[<Interface>]
type IRouteLinks =

  abstract IsActive: name: string * parameters: RouteParams -> bool

  abstract TryBuild: name: string * parameters: RouteParams -> Result<string, string>

type RouterSnapshot<'View> =
  { State: NavigationState
    Route: RouteContext voption
    Content: 'View voption
    History: HistorySnapshot }

[<Interface>]
type IRouter<'View> =
  inherit INavigable

  abstract Changed: IEvent<RouterSnapshot<'View>>

  abstract ClearCache: unit -> unit

  abstract History: IHistory

  abstract Links: IRouteLinks

  abstract Snapshot: RouterSnapshot<'View>

type GlobalGuard =
  RouteContext voption -> RouteContext -> System.Threading.CancellationToken -> System.Threading.Tasks.Task<GuardResult>

[<Interface>]
type IViewCache<'View> =

  abstract Clear: unit -> unit

  abstract Invalidate: key: string -> unit

  abstract Set: key: string * context: RouteContext * view: 'View -> unit

  abstract TryGet: key: string -> ('View * RouteContext) option

type RouterConfig<'View> =
  { Routes: RouteDefinition<'View> list
    History: IHistoryStore
    Splash: 'View option
    Guards: GlobalGuard list
    Cache: CachePolicy
    Concurrency: ConcurrencyPolicy
    SameUrl: SameUrlPolicy
    MaxRedirects: int
    Logger: Microsoft.Extensions.Logging.ILogger option }

type RouteTableError =
  | EmptyRoutes
  | EmptyRouteName
  | DuplicateRouteName of name: string
  | DuplicatePattern of pattern: string
  | AmbiguousPattern of first: string * second: string
  | InvalidPattern of pattern: string * message: string
