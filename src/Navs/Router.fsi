namespace Navs.Router

open System.Runtime.InteropServices
open FSharp.Data.Adaptive
open UrlTemplates.UrlParser

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
  [<CompiledName "Build">]
  static member build: routes: RouteDefinition<'View> seq * [<Optional>] ?splash: (unit -> 'View) -> IRouter<'View>
