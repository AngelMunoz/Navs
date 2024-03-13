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

/// <summary>
/// This object is a container for the routing operations performed by <see cref="T:Navs.INavigable`1">INavigable</see>
/// interface.
/// </summary>
[<NoComparison; NoEquality>]
type internal RoutingEnv<'View> =
  { routes: RouteTrack<'View> seq
    history: IHistoryManager<RouteTrack<'View>>
    viewCache: cmap<string, ActiveRouteParams list * 'View>
    activeRoute: cval<voption<(RouteContext * (RouteGuard * RouteDefinition<'View>) list)>>
    content: cval<voption<'View>> }


[<Sealed; Class>]
type Router =

  /// <summary>
  /// Get an instance of <see cref="T:Navs.IRouter`1">IRouter</see> with the given routes.
  /// and optionally a splash screen.
  /// </summary>
  [<CompiledName "Get">]
  static member get: routes: RouteDefinition<'View> seq * [<Optional>] ?splash: (unit -> 'View) -> IRouter<'View>
