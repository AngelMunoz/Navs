namespace Navs

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open System.Collections.Generic
open FSharp.Data.Adaptive
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser

[<Interface>]
type IDisposableBag =
  inherit IDisposable
  abstract AddDisposable: IDisposable -> unit

[<NoComparison; NoEquality>]
type RouteContext = {
  [<CompiledName "Path">]
  path: string
  [<CompiledName "UrlMatch">]
  urlMatch: UrlMatch
  [<CompiledName "UrlInfo">]
  urlInfo: UrlInfo
  [<CompiledName "Disposables">]
  disposables: IDisposableBag

} with

  [<CompiledName "AddDisposable">]
  member this.addDisposable disposable =
    this.disposables.AddDisposable disposable

module RouteContext =
  let addDisposable disposable (ctx: RouteContext) =
    ctx.addDisposable disposable

[<Struct; NoComparison; NoEquality>]
type NavigationError<'View> =
  | SameRouteNavigation
  | NavigationCancelled
  | RouteNotFound of url: string
  | NavigationFailed of message: string
  | CantDeactivate of deactivatedRoute: string
  | CantActivate of activatedRoute: string
  | GuardRedirect of redirectTo: string

[<Struct; NoComparison>]
type NavigationState =
  | Idle
  | Navigating

[<Interface>]
type INavigable<'View> =

  abstract member State: aval<NavigationState>

  abstract member StateSnapshot: NavigationState

  abstract member Navigate:
    url: string * [<Optional>] ?cancellationToken: CancellationToken ->
      Task<Result<unit, NavigationError<'View>>>

  abstract member NavigateByName:
    routeName: string *
    [<Optional>] ?routeParams: IReadOnlyDictionary<string, obj> *
    [<Optional>] ?cancellationToken: CancellationToken ->
      Task<Result<unit, NavigationError<'View>>>

[<Interface>]
type IRouter<'View> =
  inherit INavigable<'View>

  abstract member Route: aval<RouteContext voption>

  abstract member RouteSnapshot: RouteContext voption

  abstract member Content: aval<'View voption>

  abstract member ContentSnapshot: 'View voption


[<Struct; NoComparison>]
type GuardResponse =
  | Continue
  | Stop
  | Redirect of url: string

type RouteGuard<'View> =
  delegate of
    RouteContext voption * RouteContext * CancellationToken ->
      ValueTask<GuardResponse>

type GetView<'View> =
  delegate of
    RouteContext * INavigable<'View> * CancellationToken -> ValueTask<'View>

[<Struct>]
type CacheStrategy =
  | NoCache
  | Cache

[<NoComparison; NoEquality>]
type RouteDefinition<'View> = {
  [<CompiledName "Name">]
  name: string
  [<CompiledName "Pattern">]
  pattern: string
  [<CompiledName "GetContent">]
  getContent: GetView<'View>
  [<CompiledName "CanActivate">]
  canActivate: RouteGuard<'View> list
  [<CompiledName "CanDeactivate">]
  canDeactivate: RouteGuard<'View> list
  [<CompiledName "CacheStrategy">]
  cacheStrategy: CacheStrategy
}
