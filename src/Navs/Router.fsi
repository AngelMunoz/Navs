namespace Navs.Router

open System.Runtime.InteropServices
open Microsoft.Extensions.Logging
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

[<Sealed; Class>]
type Router =

  /// <summary>
  /// Get an instance of <see cref="T:Navs.IRouter`1">IRouter</see> with the given routes.
  /// and optionally a splash screen.
  /// </summary>
  /// <param name="routes">The route definitions the router will use to match URLs and render views.</param>
  /// <param name="splash">
  /// An optional function that produces a view to render while the first
  /// navigation is triggered, before any route has been activated.
  /// </param>
  /// <param name="logger">An optional logger used to trace the router's activity.</param>
  /// <param name="maxCyclicRedirects">
  /// Maximum number of distinct (from, target) redirect pairs the router will
  /// follow while resolving a single navigation. Defaults to 5. This guards
  /// against redirect cycles; it does not cap the length of linear redirect
  /// chains, which are expected to terminate on their own.
  /// </param>
  [<CompiledName "Build">]
  static member build:
    routes: RouteDefinition<'View> seq *
    [<Optional>] ?splash: (unit -> 'View) *
    [<Optional>] ?logger: ILogger *
    [<Optional>] ?maxCyclicRedirects: int ->
      IRouter<'View>
