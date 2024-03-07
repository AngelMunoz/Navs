namespace Navs.Router

open System
open System.Collections.Generic
open System.Threading
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
/// This is the orchestrating object that manages the navigation, history, rendered views
/// and tracks which route is currently active.
/// </summary>
type Router<'View> =
  /// <param name="routes">The routes that the router will use to match the URL and render the view</param>
  /// <param name="splash">
  /// The router initially doesn't have a view to render. You can provide this function
  /// to supply a splash-like (like mobile devices initial screen) view to render while you trigger the first navigation.
  /// </param>
  /// <param name="notFound">The view that will be rendered when the router cannot find a route to match the URL</param>
  /// <param name="historyManager">The history manager that the router will use to manage the navigation history</param>
  new:
    routes: RouteTrack<'View> seq *
    [<Optional>] ?splash: Func<INavigate<'View>, 'View> *
    [<Optional>] ?notFound: Func<INavigate<'View>, 'View> *
    [<Optional>] ?historyManager: IHistoryManager<RouteTrack<'View>> ->
      Router<'View>

  /// <summary>
  /// The current view that is being rendered by the router.
  /// </summary>
  /// <remarks>
  /// This observable only emits views when the internal navigation is successful
  /// otherwise, if it fails internally, it won't emit any new view to avoid blank screens.
  ///
  /// This observable is powered by an adaptive data source.
  /// </remarks>
  ///
  member Content: IObservable<'View>


  /// <summary>
  /// The current route that is being rendered by the router.
  /// </summary>
  /// <remarks>
  /// This adaptive value will emit the current route that is being rendered by the router.
  /// It will also however emit None when the router is in a state where it doesn't have a route to render,
  /// this could be when the router is just starting up and hasn't navigated to any route yet or when the router
  /// failed to navigate.
  ///
  /// It is recommended to use the Content observable to track the view that is being rendered by the router
  /// unless you are in an environment that supports adaptive values.
  /// </remarks>
  member AdaptiveContent: aval<'View option>

  /// <summary>
  /// Performs a navigation to the route that matches the URL.
  /// </summary>
  /// <param name="url">The URL to navigate to</param>
  /// <param name="cancellationToken">A token that can be used to cancel the navigation</param>
  /// <returns>
  /// A task that will complete when the navigation is successful or when it fails.
  /// </returns>
  member Navigate:
    url: string * [<Optional>] ?cancellationToken: CancellationToken -> Tasks.Task<Result<unit, NavigationError<'View>>>

  /// <summary>
  /// Performs a navigation by the name of the route.
  /// </summary>
  /// <param name="routeName">The name of the route to navigate to</param>
  /// <param name="routeParams">The parameters that will be used to match the route</param>
  /// <param name="cancellationToken">A token that can be used to cancel the navigation</param>
  /// <returns>
  /// A task that will complete when the navigation is successful or when it fails.
  /// </returns>
  /// <remarks>
  /// The route name is the name that was used to define the route in the route definition.
  ///
  /// Please note that any required parameters not supplied in the routeParams will make the navigation fail.
  /// </remarks>
  member NavigateByName:
    routeName: string *
    [<Optional>] ?routeParams: IReadOnlyDictionary<string, obj> *
    [<Optional>] ?cancellationToken: CancellationToken ->
      Tasks.Task<Result<unit, NavigationError<'View>>>

  interface INavigate<'View>
