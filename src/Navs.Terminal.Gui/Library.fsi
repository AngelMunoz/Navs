namespace Navs.Terminal.Gui


open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open FSharp.Data.Adaptive
open Navs
open Terminal.Gui

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

  module Interop =

    /// <summary>
    /// Provide a dotnet interop friendly interface to handle local state via Adaptive data
    /// </summary>
    val UseState<'Value> : initialValue: 'Value -> struct (aval<'Value> * Action<Func<'Value, 'Value>>)

/// <summary>
/// dotnet interop friendly API for adaptive and changeable data
/// </summary>
[<Class>]
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
/// A router that is specialized to work with Terminal.Gui types.
/// This router will render any object that inherits from Terminal.Gui's of Window.
/// </summary>
type TerminalGuiRouter =
  /// <param name="routes">The routes that the router will use to match the URL and render the view</param>
  /// <param name="splash">
  /// The router initially doesn't have a view to render. You can provide this function
  /// to supply a splash-like (like mobile devices initial screen) view to render while you trigger the first navigation.
  /// </param>
  new: routes: RouteDefinition<Window> seq * [<Optional>] ?splash: Func<Window> -> TerminalGuiRouter

  interface IRouter<Window>



[<Class; Sealed>]
type Route =

  ///<summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">The view to render when the route is activated</param>
  /// <returns>A route definition</returns>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<Window> -> Async<#Window>) ->
      RouteDefinition<Window>

  /// <summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">An task returning function to render when the route is activated.</param>
  /// <returns>A route definition</returns>
  /// <remarks>A cancellation token is provided alongside the route context to allow you to support cancellation of the route activation.</remarks>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<Window> -> CancellationToken -> Task<#Window>) ->
      RouteDefinition<Window>

  ///<summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">The view to render when the route is activated</param>
  /// <returns>A route definition</returns>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<Window> -> #Window) -> RouteDefinition<Window>

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
      name: string * path: string * handler: Func<RouteContext, INavigable<Window>, #Window> -> RouteDefinition<Window>

    /// <summary>Defines a route in the application</summary>
    /// <param name="name">The name of the route</param>
    /// <param name="path">A templated URL that will be used to match this route</param>
    /// <param name="handler">An task returning function to render when the route is activated.</param>
    /// <returns>A route definition</returns>
    /// <remarks>A cancellation token is provided alongside the route context to allow you to support cancellation of the route activation.</remarks>
    [<CompiledName "Define">]
    static member inline define:
      name: string * path: string * handler: Func<RouteContext, INavigable<Window>, CancellationToken, Task<#Window>> ->
        RouteDefinition<Window>

[<Class>]
type RouterOutlet =
  inherit Toplevel

  new: router: IRouter<Window> -> RouterOutlet
