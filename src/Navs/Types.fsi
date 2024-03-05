namespace Navs

open System
open System.Threading
open System.Threading.Tasks
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser


type RouteContext =
  { Route: string
    UrlMatch: UrlMatch
    UrlInfo: UrlInfo }

type RouteGuard = Func<RouteContext, CancellationToken, Task<bool>>
type GetView<'View> = Func<RouteContext, CancellationToken, Task<'View>>

[<Struct>]
type CacheStrategy =
  | NoCache
  | Cache

[<NoComparison; NoEquality>]
type RouteDefinition<'View> =
  { Name: string
    Pattern: string
    GetContent: GetView<'View>
    Children: RouteDefinition<'View> list
    CanActivate: RouteGuard list
    CanDeactivate: RouteGuard list
    CacheStrategy: CacheStrategy }

[<NoComparison; NoEquality>]
type RouteTrack<'View> =
  { PatternPath: string
    Definition: RouteDefinition<'View>
    ParentTrack: RouteTrack<'View> voption
    Children: RouteTrack<'View> list }

module RouteTracks =

  [<CompiledName "FromDefinitions">]
  val fromDefinitions: routes: RouteDefinition<'View> seq -> RouteTrack<'View> list
