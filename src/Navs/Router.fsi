namespace Navs.Router

open System.Runtime.InteropServices
open FSharp.Data.Adaptive

open Navs

/// <summary>
/// This object contains parameters and its values
/// that are extracted from the URL when a route is activated.
/// </summary>
/// <remarks>
/// Note that every parameter here is a string as it is extracted from the URL.
/// As it has not been processed by the route yet.
/// </remarks>
[<Struct; NoComparison>]
type ActiveRouteParams =
  { SegmentIndex: int
    ParamName: string
    ParamValue: string }

[<NoComparison; NoEquality>]
type RoutingEnv<'View> =
  { routes: RouteTrack<'View> seq
    history: IHistoryManager<RouteTrack<'View>>
    viewCache: cmap<string, ActiveRouteParams list * 'View>
    activeRoute: cval<voption<(RouteContext * (RouteGuard * RouteDefinition<'View>) list)>>
    content: cval<voption<'View>> }


[<RequireQualifiedAccess>]
module RoutingEnv =
  val get<'View> : routes: RouteDefinition<'View> seq * splash: (unit -> 'View) option -> RoutingEnv<'View>

[<RequireQualifiedAccess>]
module Navigable =
  val get<'View> : routingEnv: RoutingEnv<'View> -> INavigable<'View>

[<Sealed; Class>]
type Router =

  static member get: env: RoutingEnv<'View> * nav: INavigable<'View> -> IRouter<'View>

  [<CompiledName "Get">]
  static member get: routes: RouteDefinition<'View> seq * [<Optional>] ?splash: (unit -> 'View) -> IRouter<'View>
