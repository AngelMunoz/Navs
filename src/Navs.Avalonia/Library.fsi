namespace Navs.Avalonia

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Avalonia
open Avalonia.Controls
open Avalonia.Data
open Avalonia.Animation

open FSharp.Data.Adaptive
open Navs

[<RequireQualifiedAccess>]
module AVal =

  /// <summary>
  /// Get the value of an adaptive value by forcing it
  /// </summary>
  val inline getValue: adaptiveValue: aval<'Value> -> 'Value

  /// <summary>
  /// sets up a transaction and sets the value of a changeable value
  /// </summary>
  val inline setValue: adaptiveValue: cval<'Value> -> value: 'Value -> unit

  /// <summary>
  /// sets up a transaction and sets the value resulting of the provided function
  /// </summary>
  val inline mapSet: adaptiveValue: cval<'Value> -> setValue: ('Value -> 'Value) -> unit

  /// <summary>
  /// Provide a friendly interface to handle local state via Adaptive data
  /// </summary>
  val useState: initialValue: 'Value -> aval<'Value> * (('Value -> 'Value) -> unit)

  /// <summary>
  /// Converts an adaptive value into an observable
  /// </summary>
  [<CompiledName "ToObservable">]
  val toObservable<'Value> : value: aval<'Value> -> IObservable<'Value>

  /// <summary>
  /// Convert Adaptive data into a binding that can be handled by avalonia
  /// </summary>
  [<CompiledName "ToBinding">]
  val toBinding<'Value> : value: aval<'Value> -> BindingBase

  module Interop =

    /// <summary>
    /// Provide a dotnet interop friendly interface to handle local state via Adaptive data
    /// </summary>
    val UseState<'Value> : initialValue: 'Value -> struct (aval<'Value> * Action<Func<'Value, 'Value>>)

/// <summary>
/// dotnet interop friendly API for adaptive and changeable data
/// </summary>
[<Extension; Class>]
type AValExtensions =

  /// <summary>
  /// Get the value of an adaptive value by forcing it
  /// </summary>
  [<CompiledName "GetValue"; Extension>]
  static member inline getValue: adaptiveValue: aval<'Value> -> 'Value

  /// <summary>
  /// sets up a transaction and sets the value of a changeable value
  /// </summary>
  [<CompiledName "SetValue"; Extension>]
  static member inline setValue: adaptiveValue: cval<'Value> * value: 'Value -> unit

  [<CompiledName "SetValue"; Extension>]
  static member inline setValue: adaptiveValue: cval<'Value> * setValue: Func<'Value, 'Value> -> unit

  /// <summary>
  /// Creates a one-way binding from an adaptive value which can be bound to Avalonia properties.
  /// </summary>
  [<CompiledName "ToBinding"; Extension>]
  static member inline toBinding: value: aval<'Value> -> BindingBase


/// <summary>
/// A router that is specialized to work with Avalonia types.
/// This router will render any object that inherits from Avalonia's of Control.
/// </summary>
type AvaloniaRouter =
  /// <param name="routes">The routes that the router will use to match the URL and render the view</param>
  /// <param name="splash">
  /// The router initially doesn't have a view to render. You can provide this function
  /// to supply a splash-like (like mobile devices initial screen) view to render while you trigger the first navigation.
  /// </param>
  /// <param name="logger">An optional logger to log the router's activity</param>
  new:
    routes: RouteDefinition<Control> seq * [<Optional>] ?splash: Func<Control> * [<Optional>] ?logger: ILogger ->
      AvaloniaRouter

  interface IRouter<Control>

[<Class>]
type Route =
  inherit UserControl

  /// <summary>
  /// Gets the definition of the route.
  /// </summary>
  member Definition: RouteDefinition<Control> with get

  /// <summary>
  /// Initializes a new instance of the Route class with a name and a handler.
  /// </summary>
  new: name: string * path: string * handler: (RouteContext -> INavigable<Control> -> Control) -> Route

  /// <summary>
  /// Initializes a new instance of the Route class with a name and an asynchronous handler.
  /// </summary>
  new: name: string * path: string * handler: (RouteContext -> INavigable<Control> -> Async<Control>) -> Route

  /// <summary>
  /// Initializes a new instance of the Route class with a name and an asynchronous handler.
  /// </summary>
  new:
    name: string * path: string * handler: (RouteContext -> INavigable<Control> -> CancellationToken -> Task<Control>) ->
      Route

  /// <summary>
  /// Initializes a new instance of the Route class with a name.
  /// </summary>
  new: def: RouteDefinition<Control> -> Route

  ///<summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">The view to render when the route is activated</param>
  /// <returns>A route definition</returns>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<Control> -> Async<Control>) ->
      RouteDefinition<Control>

  /// <summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">An task returning function to render when the route is activated.</param>
  /// <returns>A route definition</returns>
  /// <remarks>A cancellation token is provided alongside the route context to allow you to support cancellation of the route activation.</remarks>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<Control> -> CancellationToken -> Task<Control>) ->
      RouteDefinition<Control>

  ///<summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">The view to render when the route is activated</param>
  /// <returns>A route definition</returns>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<Control> -> Control) -> RouteDefinition<Control>

/// <summary>
/// Represents a collection of routes in the application.
/// </summary>
[<Class>]
type Routes =
  inherit UserControl

  /// <summary>
  /// Initializes a new instance of the Routes class with an initial URI and an optional logger.
  /// /// The initial URI is used to set the initial route when the application starts.
  /// </summary>
  /// <param name="initialUri">The initial URI to set the route to when the router starts.</param>
  /// <param name="noView">An optional control to render when no route is matched.</param>
  /// <param name="logger">An optional logger to log the router's activity.</param>
  /// <remarks>
  /// If you don't provide an initial URI, the navigation will default to "/"
  /// if the route is not found the noView control will be rendered.
  /// And you will have to programmatically navigate to a route using the `Navigate` method.
  /// </remarks>
  new: [<Optional>] ?initialUri: string * [<Optional>] ?noView: Control * [<Optional>] ?logger: ILogger -> Routes

  /// <summary>
  /// Gets or sets the children routes.
  /// </summary>
  member Children: Route[] with get, set
  member Router: IRouter<Control> voption with get

  /// <summary>
  /// Gets the property for children routes.
  /// </summary>
  static member ChildrenProperty: DirectProperty<Routes, Route[]>

[<Extension; Class>]
type RoutesExtensions =

  [<Extension>]
  static member inline Children: control: Routes * [<ParamArray>] routes: Route[] -> Routes

/// <summary>
/// A module that contains the interop functions to use the Route class from other languages.
/// It is mostly the same API but using Func instead of F# function types.
/// </summary>
module Interop =

  [<Class>]
  type Route =
    ///<summary>Defines a route in the application</summary>
    /// <param name="name">The name of the route</param>
    /// <param name="path">A templated URL that will be used to match this route</param>
    /// <param name="handler">The view to render when the route is activated</param>
    /// <returns>A route definition</returns>
    [<CompiledName "Define">]
    static member inline define:
      name: string * path: string * handler: Func<RouteContext, INavigable<Control>, Control> ->
        RouteDefinition<Control>

    /// <summary>Defines a route in the application</summary>
    /// <param name="name">The name of the route</param>
    /// <param name="path">A templated URL that will be used to match this route</param>
    /// <param name="handler">An task returning function to render when the route is activated.</param>
    /// <returns>A route definition</returns>
    /// <remarks>A cancellation token is provided alongside the route context to allow you to support cancellation of the route activation.</remarks>
    [<CompiledName "Define">]
    static member inline define:
      name: string * path: string * handler: Func<RouteContext, INavigable<Control>, CancellationToken, Task<Control>> ->
        RouteDefinition<Control>

[<Class>]
type RouterOutlet =
  inherit UserControl

  static member RouterProperty: DirectProperty<RouterOutlet, IRouter<Control> | null>

  static member PageTransitionProperty: DirectProperty<RouterOutlet, IPageTransition | null>

  static member NoContentProperty: DirectProperty<RouterOutlet, Control | null>

  new: unit -> RouterOutlet

  member Router: IRouter<Control> | null with get, set

  member PageTransition: IPageTransition | null with get, set

  member NoContent: Control | null with get, set

[<Extension; Class>]
type RouterOutletExtensions =

  [<Extension>]
  static member RouterOutlet: unit -> RouterOutlet

  [<Extension; CompiledName "Router">]
  static member inline router: routerOutlet: RouterOutlet * router: IRouter<Control> -> RouterOutlet

  [<Extension; CompiledName "PageTransition">]
  static member inline pageTransition: routerOutlet: RouterOutlet * pageTransition: IPageTransition -> RouterOutlet

  [<Extension; CompiledName "PageTransition">]
  static member inline pageTransition:
    routerOutlet: RouterOutlet *
    pageTransition: aval<IPageTransition> *
    [<Optional>] ?mode: BindingMode *
    [<Optional>] ?priority: BindingPriority ->
      RouterOutlet

  [<Extension; CompiledName "NoContent">]
  static member inline noContent: routerOutlet: RouterOutlet * noContent: Control -> RouterOutlet

  [<Extension; CompiledName "NoContent">]
  static member inline noContent:
    routerOutlet: RouterOutlet *
    noContent: aval<Control> *
    [<Optional>] ?mode: BindingMode *
    [<Optional>] ?priority: BindingPriority ->
      RouterOutlet
