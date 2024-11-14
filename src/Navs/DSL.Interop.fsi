namespace Navs.Interop

open System
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

open Navs

[<Class>]
type Route =
  /// <summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="getContent">The delegate that will be called to render the view when the route is activated</param>
  /// <returns>A route definition</returns>
  /// <remarks>
  ///  This function should ideally be used from non-F# languages as it provides a more standard Function signature.
  /// </remarks>
  static member inline Define:
    name: string * path: string * getContent: Func<RouteContext, INavigable<'View>, 'View> -> RouteDefinition<'View>

  /// <summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="getContent">The delegate that will be called to render the view when the route is activated</param>
  /// <returns>A route definition</returns>
  /// <remarks>
  /// This function should ideally be used from F# as it provides a more idiomatic F# Function signature.
  /// </remarks>
  static member inline Define:
    name: string * path: string * getContent: Func<RouteContext, INavigable<'View>, CancellationToken, Task<'View>> ->
      RouteDefinition<'View>

/// <summary>
/// A module that provides functions for route guard responses
/// </summary>
[<RequireQualifiedAccess>]
module Guard =

  /// <summary>
  /// A guard response that indicates that the navigation should continue
  /// </summary>
  val inline Continue: unit -> GuardResponse

  /// <summary>
  /// A guard response that indicates that the navigation should stop and fail the navigation
  /// </summary>
  val inline Stop: unit -> GuardResponse

  /// <summary>
  /// A guard response that indicates that the navigation should stop and redirect to the specified URL
  /// </summary>
  val inline Redirect: string -> GuardResponse

/// <summary>
/// Extensions for a builder-like API for defining routes in the application
/// </summary>
[<Class; Extension>]
type RouteDefinitionExtensions =

  /// <summary>
  /// Takes a sequence of route guards and adds them to the route definition as guards that will be executed when the route is activated.
  /// </summary>
  [<Extension>]
  static member inline CanActivate:
    routeDef: RouteDefinition<'View> * [<ParamArray>] guards: Func<RouteContext | null, RouteContext, GuardResponse> array ->
      RouteDefinition<'View>

  /// <summary>
  /// Takes a sequence of route guards and adds them to the route definition as guards that will be executed when the route is activated.
  /// </summary>
  [<Extension>]
  static member inline CanActivate:
    routeDef: RouteDefinition<'View> *
    [<ParamArray>] guards: Func<RouteContext | null, RouteContext, CancellationToken, Task<GuardResponse>> array ->
      RouteDefinition<'View>

  /// <summary>
  /// Takes a sequence of route guards and adds them to the route definition as guards that will be executed when the route is activated.
  /// </summary>
  [<Extension>]
  static member inline CanDeactivate:
    routeDef: RouteDefinition<'View> * [<ParamArray>] guards: Func<RouteContext | null, RouteContext, GuardResponse> array ->
      RouteDefinition<'View>

  /// <summary>
  /// Takes a sequence of route guards and adds them to the route definition as guards that will be executed when the route is deactivated.
  /// </summary>
  [<Extension>]
  static member inline CanDeactivate:
    routeDef: RouteDefinition<'View> *
    [<ParamArray>] guards: Func<RouteContext | null, RouteContext, CancellationToken, Task<GuardResponse>> array ->
      RouteDefinition<'View>

  /// <summary>
  /// Ensure that rendered view used for this route is picked up from the in-memory cache.
  /// </summary>
  [<Extension>]
  static member inline CacheOnVisit: routeDef: RouteDefinition<'View> -> RouteDefinition<'View>

  /// <summary>
  /// Ensure that rendered view used for this route is always re-rendered when the route is activated.
  /// </summary>
  [<Extension>]
  static member inline NoCacheOnVisit: routeDef: RouteDefinition<'View> -> RouteDefinition<'View>
