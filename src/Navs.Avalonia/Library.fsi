namespace Navs.Avalonia

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

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
  val toBinding<'Value> : value: aval<'Value> -> IBinding

  module Interop =

    /// <summary>
    /// Provide a dotnet interop friendly interface to handle local state via Adaptive data
    /// </summary>
    val UseState<'Value> : initialValue: 'Value -> struct (aval<'Value> * Action<Func<'Value, 'Value>>)

[<RequireQualifiedAccess>]
module CVal =

  /// <summary>
  /// Provides a double way binding for changeable values
  /// </summary>
  /// <remarks>
  /// This binding is read-write and can be used to bind to properties that support two-way binding.
  /// If you're looking to just use a readonly binding, use the `toBinding` method with the AVal module instead.
  /// </remarks>
  [<CompiledName "ToBinding";
    Experimental "Incompatible for Avalonia v11.1+, we're waiting for a replacement in/before v12.">]
  val toBinding<'Value> : cval<'Value> -> IBinding

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
  static member inline toBinding: value: aval<'Value> -> IBinding

  /// <summary>
  /// Creates a two-way binding from a changeable value which can be bound to Avalonia properties.
  /// </summary>
  /// <remarks>
  /// This binding is read-write and can be used to bind to properties that support two-way binding.
  /// If you're looking to just use a readonly binding, use the `toBinding` method with an aval instead.
  /// </remarks>
  [<CompiledName "ToBinding";
    Extension;
    Experimental "Incompatible for Avalonia v11.1+, we're waiting for a replacement in/before v12.">]
  static member inline toBinding: value: cval<'Value> -> IBinding


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
  new: routes: RouteDefinition<Control> seq * [<Optional>] ?splash: Func<Control> -> AvaloniaRouter

  interface IRouter<Control>

[<Class; Sealed>]
type Route =

  ///<summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">The view to render when the route is activated</param>
  /// <returns>A route definition</returns>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<Control> -> Async<#Control>) ->
      RouteDefinition<Control>

  /// <summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">An task returning function to render when the route is activated.</param>
  /// <returns>A route definition</returns>
  /// <remarks>A cancellation token is provided alongside the route context to allow you to support cancellation of the route activation.</remarks>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<Control> -> CancellationToken -> Task<#Control>) ->
      RouteDefinition<Control>

  ///<summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">The view to render when the route is activated</param>
  /// <returns>A route definition</returns>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<Control> -> #Control) -> RouteDefinition<Control>

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
      name: string * path: string * handler: Func<RouteContext, INavigable<Control>, #Control> ->
        RouteDefinition<Control>

    /// <summary>Defines a route in the application</summary>
    /// <param name="name">The name of the route</param>
    /// <param name="path">A templated URL that will be used to match this route</param>
    /// <param name="handler">An task returning function to render when the route is activated.</param>
    /// <returns>A route definition</returns>
    /// <remarks>A cancellation token is provided alongside the route context to allow you to support cancellation of the route activation.</remarks>
    [<CompiledName "Define">]
    static member inline define:
      name: string * path: string * handler: Func<RouteContext, INavigable<Control>, CancellationToken, Task<#Control>> ->
        RouteDefinition<Control>

[<Class>]
type RouterOutlet =
  inherit UserControl

  static member RouterProperty: DirectProperty<RouterOutlet, IRouter<Control>>

  static member PageTransitionProperty: DirectProperty<RouterOutlet, IPageTransition>

  static member NoContentProperty: DirectProperty<RouterOutlet, Control>

  new: unit -> RouterOutlet

  member Router: IRouter<Control> with get, set

  member PageTransition: IPageTransition with get, set

  member NoContent: Control with get, set

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
