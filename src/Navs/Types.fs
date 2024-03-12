namespace Navs

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open System.Collections.Generic
open FSharp.Data.Adaptive
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser

type RouteContext = {
  path: string
  urlMatch: UrlMatch
  urlInfo: UrlInfo
}

[<Struct; NoComparison; NoEquality>]
type NavigationError<'View> =
  | NavigationCancelled
  | RouteNotFound of url: string
  | CantDeactivate of deactivatedRoute: string
  | CantActivate of activatedRoute: string

[<Interface>]
type INavigable<'View> =

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

  abstract member Content: aval<'View voption>

type RouteGuard = RouteContext -> CancellationToken -> Task<bool>

type GetView<'View> =
  RouteContext -> INavigable<'View> -> CancellationToken -> Task<'View>

[<Struct>]
type CacheStrategy =
  | NoCache
  | Cache

[<NoComparison; NoEquality>]
type RouteDefinition<'View> = {
  name: string
  pattern: string
  getContent: GetView<'View>
  children: RouteDefinition<'View> list
  canActivate: RouteGuard list
  canDeactivate: RouteGuard list
  cacheStrategy: CacheStrategy
}

[<NoComparison; NoEquality>]
type RouteTrack<'View> = {
  pathPattern: string
  routeDefinition: RouteDefinition<'View>
  parentTrack: RouteTrack<'View> voption
  children: RouteTrack<'View> list
}
