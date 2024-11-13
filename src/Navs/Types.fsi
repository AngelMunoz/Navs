namespace Navs

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open System.Collections.Generic
open IcedTasks
open FSharp.Data.Adaptive
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser

/// <summary>
/// An object that contains multiple disposable objects that can be disposed of
/// when the route is not cached and deactivated.
/// </summary>
[<Interface>]
type IDisposableBag =
  inherit IDisposable
  abstract AddDisposable: IDisposable -> unit

/// <summary>
/// The context of the route that is being activated.
/// This can be used to extract the parameters from the URL and extract information
/// about the templated route that is being activated.
/// </summary>
[<NoComparison; NoEquality>]
type RouteContext =
  {
    /// RAW URL that is being activated
    [<CompiledName "Path">]
    path: string
    /// An object that contains multiple dictionaries with the parameters
    /// that were extracted from the URL either from the url parameters
    /// the query string or the hash portion of the URL.
    [<CompiledName "UrlMatch">]
    urlMatch: UrlMatch
    /// An object that contains the segments, query and hash of the URL in a string form.
    [<CompiledName "UrlInfo">]
    urlInfo: UrlInfo
    /// An object that contains objects that should be disposed of when the route is deactivated.
    [<CompiledName "Disposables">]
    disposables: IDisposableBag
  }

  [<CompiledName "AddDisposable">]
  member addDisposable: IDisposable -> unit

module RouteContext =
  val addDisposable: IDisposable -> RouteContext -> unit

/// <summary>
/// This object contains the contextual information about why a navigation
/// could not be performed.
/// </summary>
[<Struct; NoComparison; NoEquality>]
type NavigationError<'View> =
  | SameRouteNavigation
  | NavigationCancelled
  | RouteNotFound of url: string
  | NavigationFailed of message: string
  | CantDeactivate of deactivatedRoute: string
  | CantActivate of activatedRoute: string
  | GuardRedirect of redirectTo: string

[<Struct; NoComparison>]
type NavigationState =
  | Idle
  | Navigating

[<Interface>]
type INavigable<'View> =

  /// <summary>
  /// The state of the router.
  /// </summary>
  /// <remarks>
  /// This adaptive value will emit the current state of the router.
  /// It will emit Navigating when the router is in the process of navigating to a route.
  /// It will emit Idle when the router is not navigating to a route.
  /// </remarks>
  abstract member State: aval<NavigationState>

  /// <summary>
  /// The current state of the router.
  /// </summary>
  /// <remarks>
  /// This property will return the current state of the router.
  /// It will return Navigating when the router is in the process of navigating to a route.
  /// It will return Idle when the router is not navigating to a route.
  /// </remarks>
  abstract member StateSnapshot: NavigationState

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
  /// </remarks>
  abstract member Route: aval<RouteContext voption>

  /// <summary>
  /// The current route that is being rendered by the router.
  /// </summary>
  /// <remarks>
  /// This property will return the current route that is being rendered by the router.
  /// It will also however return None when the router is in a state where it doesn't have a route to render,
  /// this could be when the router is just starting up and hasn't navigated to any route yet or when the router
  /// failed to navigate.
  /// </remarks>
  abstract member RouteSnapshot: RouteContext voption

  /// <summary>
  /// The current route that is being rendered by the router.
  /// </summary>
  /// <remarks>
  /// This adaptive value will emit the current route that is being rendered by the router.
  /// </remarks>
  abstract member Content: aval<'View voption>

  /// <summary>
  /// The current route that is being rendered by the router.
  /// </summary>
  /// <remarks>
  /// This is a single value that will return the current route that is being rendered by the router.
  /// </remarks>
  abstract member ContentSnapshot: 'View voption

[<Struct; NoComparison>]
type GuardResponse =
  | Continue
  | Stop
  | Redirect of url: string

/// An alias for a function that takes a route context and a cancellation token
/// In order to determine if the route can be activated/deactivated or not.
type RouteGuard<'View> = delegate of RouteContext voption * RouteContext -> CancellableValueTask<GuardResponse>

/// An alias for a function that takes a route context and a cancellation token
/// in order to extract the view that will be rendered when the route is activated.
type GetView<'View> = delegate of RouteContext * INavigable<'View> -> CancellableValueTask<'View>

/// The strategy that the router will use to cache the views that are rendered
/// when the route is activated.
[<Struct; NoComparison>]
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
    [<CompiledName "Name">]
    name: string
    /// The URL pattern that will be used to match the route and enforce
    /// the URL's parameters to be extracted and passed to the view.
    [<CompiledName "Pattern">]
    pattern: string
    /// The delegate that will be called to render the view when the route is activated.
    [<CompiledName "GetContent">]
    getContent: GetView<'View>
    /// The guards that will be executed when the route is activated.
    /// If any of them returns false, the route will not be activated.
    [<CompiledName "CanActivate">]
    canActivate: RouteGuard<'View> list
    /// The guards that will be executed when the route is deactivated.
    /// If any of them returns false, the route will not be deactivated.
    [<CompiledName "CanDeactivate">]
    canDeactivate: RouteGuard<'View> list
    /// The strategy that the router will use to cache the views that are rendered
    /// when the route is activated.
    [<CompiledName "CacheStrategy">]
    cacheStrategy: CacheStrategy
  }
