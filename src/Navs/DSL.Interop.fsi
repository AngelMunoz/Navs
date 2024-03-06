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
    name: string * path: string * getContent: Func<RouteContext, 'View> -> RouteDefinition<'View>

  /// <summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="getContent">The asynchronous delegate that will be called to render the view when the route is activated</param>
  /// <returns>A route definition</returns>
  /// <remarks>
  ///  This function should ideally be used from non-F# languages as it provides a more standard Function signature.
  /// </remarks>
  static member inline Define:
    name: string * path: string * getContent: Func<RouteContext, CancellationToken, Task<'View>> ->
      RouteDefinition<'View>

/// <summary>
/// Extensions for a builder-like API for defining routes in the application
/// </summary>
[<Class; Extension>]
type RouteDefinitionExtensions =

  /// <summary>
  /// Takes a route definition and adds it as a child of the parent route definition.
  /// </summary>
  [<Extension>]
  static member inline Child: routeDef: RouteDefinition<'View> * child: RouteDefinition<'View> -> RouteDefinition<'View>

  /// <summary>
  /// Takes a sequence of route definitions and adds them as children of the parent route definition.
  /// </summary>
  [<Extension>]
  static member inline Children:
    routeDef: RouteDefinition<'View> * [<ParamArray>] children: RouteDefinition<'View> array -> RouteDefinition<'View>

  /// <summary>
  /// Takes a sequence of route guards and adds them to the route definition as guards that will be executed when the route is activated.
  /// </summary>
  [<Extension>]
  static member inline CanActivate:
    routeDef: RouteDefinition<'View> * [<ParamArray>] guards: RouteGuard array -> RouteDefinition<'View>

  /// <summary>
  /// Takes a sequence of route guards and adds them to the route definition as guards that will be executed when the route is deactivated.
  /// </summary>
  [<Extension>]
  static member inline CanDeactivate:
    routeDef: RouteDefinition<'View> * [<ParamArray>] guards: RouteGuard array -> RouteDefinition<'View>

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
