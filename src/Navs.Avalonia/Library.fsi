namespace Navs.Avalonia

open System
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks

open Avalonia.Controls
open Avalonia.Data

open FSharp.Data.Adaptive
open Navs

module AVal =
  val useState<'Value> : initialValue: 'Value -> aval<'Value> * ('Value -> unit)

  [<CompiledName "ToBinding">]
  val toBinding<'Value> : value: aval<'Value> -> IBinding

  module Interop =
    val UseState<'Value> : initialValue: 'Value -> aval<'Value> * Action<'Value>


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
