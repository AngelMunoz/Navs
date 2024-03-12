namespace Navs.FuncUI

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks

open Avalonia.FuncUI
open Avalonia.FuncUI.Types

open Navs
open Navs.Router

/// <summary>
/// A router that is specialized to work with Avalonia.FuncUI types.
/// This router will render any object that implements the IView interface.
/// </summary>
type FuncUIRouter =
  /// <param name="routes">The routes that the router will use to match the URL and render the view</param>
  /// <param name="splash">
  /// The router initially doesn't have a view to render. You can provide this function
  /// to supply a splash-like (like mobile devices initial screen) view to render while you trigger the first navigation.
  /// </param>
  new: routes: RouteDefinition<IView> seq * [<Optional>] ?splash: Func<IView> -> FuncUIRouter

  interface IRouter<IView>

[<Class; Extension>]
type IComponentContexExtensions =
  /// Subscribes to the router and returns an IReadable that emits the view that is being rendered.
  [<Extension>]
  static member inline useRouter: context: IComponentContext * router: IRouter<IView> -> IReadable<IView>

[<Class>]
type Route =
  ///<summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">The view to render when the route is activated</param>
  /// <returns>A route definition</returns>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<IView> -> Async<#IView>) ->
      RouteDefinition<IView>

  /// <summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">An task returning function to render when the route is activated.</param>
  /// <returns>A route definition</returns>
  /// <remarks>A cancellation token is provided alongside the route context to allow you to support cancellation of the route activation.</remarks>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<IView> -> CancellationToken -> Task<#IView>) ->
      RouteDefinition<IView>

  ///<summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">The view to render when the route is activated</param>
  /// <returns>A route definition</returns>
  static member define:
    name: string * path: string * handler: (RouteContext -> INavigable<IView> -> #IView) -> RouteDefinition<IView>
