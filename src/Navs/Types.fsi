namespace Navs

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open System.Collections.Generic
open FSharp.Data.Adaptive
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser

/// <summary>
/// The context of the route that is being activated.
/// This can be used to extract the parameters from the URL and extract information
/// about the templated route that is being activated.
/// </summary>
type RouteContext =
  {
    /// RAW URL that is being activated
    path: string
    /// An object that contains multiple dictionaries with the parameters
    /// that were extracted from the URL either from the url parameters
    /// the query string or the hash portion of the URL.
    urlMatch: UrlMatch
    /// An object that contains the segments, query and hash of the URL in a string form.
    urlInfo: UrlInfo
  }

/// <summary>
/// This object contains the contextual information about why a navigation
/// could not be performed.
/// </summary>
[<Struct; NoComparison; NoEquality>]
type NavigationError<'View> =
  | NavigationCancelled
  | RouteNotFound of url: string
  | CantDeactivate of deactivatedRoute: string
  | CantActivate of activatedRoute: string

[<Interface>]
type INavigable<'View> =

  /// <summary>
  /// Performs a navigation to the route that matches the URL.
  /// </summary>
  /// <param name="url">The URL to navigate to</param>
  /// <param name="cancellationToken">A token that can be used to cancel the navigation</param>
  /// <returns>
  /// A task that will complete when the navigation is successful or when it fails.
  /// </returns>
  abstract member Navigate:
    url: string * [<Optional>] ?cancellationToken: CancellationToken -> Task<Result<unit, NavigationError<'View>>>

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
  abstract member NavigateByName:
    routeName: string *
    [<Optional>] ?routeParams: IReadOnlyDictionary<string, obj> *
    [<Optional>] ?cancellationToken: CancellationToken ->
      Task<Result<unit, NavigationError<'View>>>

[<Interface>]
type IRouter<'View> =
  inherit INavigable<'View>

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
  abstract member Content: aval<'View voption>


/// An alias for a function that takes a route context and a cancellation token
/// In order to determine if the route can be activated/deactivated or not.
type RouteGuard = RouteContext -> CancellationToken -> Task<bool>

/// An alias for a function that takes a route context and a cancellation token
/// in order to extract the view that will be rendered when the route is activated.
type GetView<'View> = RouteContext -> INavigable<'View> -> CancellationToken -> Task<'View>

/// The strategy that the router will use to cache the views that are rendered
/// when the route is activated.
[<Struct>]
type CacheStrategy =
  /// The Cache strategy makes that the rendered view will be stored in memory
  /// and will be re-used when the route is activated again.
  | NoCache
  /// The NoCache strategy makes that the rendered view will be re-rendered
  /// every time the route is activated.
  | Cache

[<NoComparison; NoEquality>]
type RouteDefinition<'View> =
  {
    /// Name used to locate this route for "by-name" route activation
    name: string
    /// The URL pattern that will be used to match the route and enforce
    /// the URL's parameters to be extracted and passed to the view.
    pattern: string
    /// The delegate that will be called to render the view when the route is activated.
    getContent: GetView<'View>
    /// The children routes that this route contains.
    children: RouteDefinition<'View> list
    /// The guards that will be executed when the route is activated.
    /// If any of them returns false, the route will not be activated.
    canActivate: RouteGuard list
    /// The guards that will be executed when the route is deactivated.
    /// If any of them returns false, the route will not be deactivated.
    canDeactivate: RouteGuard list
    /// The strategy that the router will use to cache the views that are rendered
    /// when the route is activated.
    cacheStrategy: CacheStrategy
  }

/// This is an object used to keep track of the routes that are defined in the application.
/// and contextual information about the route that is being activated.
/// This is used to match the URL and render the view.
/// It also contains the children routes that are defined in the application.
[<NoComparison; NoEquality>]
type RouteTrack<'View> =
  { pathPattern: string
    routeDefinition: RouteDefinition<'View>
    parentTrack: RouteTrack<'View> voption
    children: RouteTrack<'View> list }
