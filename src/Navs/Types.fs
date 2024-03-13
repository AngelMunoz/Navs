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
  [<CompiledName "Path">]
  path: string
  [<CompiledName "UrlMatch">]
  urlMatch: UrlMatch
  [<CompiledName "UrlInfo">]
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
  [<CompiledName "Name">]
  name: string
  [<CompiledName "Pattern">]
  pattern: string
  [<CompiledName "GetContent">]
  getContent: GetView<'View>
  [<CompiledName "Children">]
  children: RouteDefinition<'View> list
  [<CompiledName "CanActivate">]
  canActivate: RouteGuard list
  [<CompiledName "CanDeactivate">]
  canDeactivate: RouteGuard list
  [<CompiledName "CacheStrategy">]
  cacheStrategy: CacheStrategy
}

[<NoComparison; NoEquality>]
type RouteTrack<'View> = {
  [<CompiledName "PathPattern">]
  pathPattern: string
  [<CompiledName "RouteDefinition">]
  routeDefinition: RouteDefinition<'View>
  [<CompiledName "ParentTrack">]
  parentTrack: RouteTrack<'View> voption
  [<CompiledName "Children">]
  children: RouteTrack<'View> list
}
