namespace Navs

open System.Collections.Generic
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser

open IcedTasks

type RouteContext =
  { Route: string
    UrlMatch: UrlMatch
    UrlInfo: UrlInfo }

type RouteGuard = RouteContext -> CancellableValueTask<bool>
type GetView<'View> = RouteContext -> CancellableValueTask<'View>

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
