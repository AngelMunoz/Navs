namespace Navs

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open System.Collections.Generic
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
    Route: string
    /// An object that contains multiple dictionaries with the parameters
    /// that were extracted from the URL either from the url parameters
    /// the query string or the hash portion of the URL.
    UrlMatch: UrlMatch
    /// An object that contains the segments, query and hash of the URL in a string form.
    UrlInfo: UrlInfo
  }

/// An alias for a function that takes a route context and a cancellation token
/// In order to determine if the route can be activated/deactivated or not.
type RouteGuard = Func<RouteContext, CancellationToken, Task<bool>>

/// An alias for a function that takes a route context and a cancellation token
/// in order to extract the view that will be rendered when the route is activated.
type GetView<'View> = Func<RouteContext, INavigate<'View>, CancellationToken, Task<'View>>

/// <summary>
/// This object contains the contextual information about why a navigation
/// could not be performed.
/// </summary>
and [<Struct; NoComparison; NoEquality>] NavigationError<'View> =
  | NavigationCancelled
  | RouteNotFound of url: string
  | CantDeactivate of deactivateGuard: RouteDefinition<'View>
  | CantActivate of activateGuard: RouteDefinition<'View>

and [<NoComparison; NoEquality>] RouteDefinition<'View> =
  {
    /// Name used to locate this route for "by-name" route activation
    Name: string
    /// The URL pattern that will be used to match the route and enforce
    /// the URL's parameters to be extracted and passed to the view.
    Pattern: string
    /// The delegate that will be called to render the view when the route is activated.
    GetContent: GetView<'View>
    /// The children routes that this route contains.
    Children: RouteDefinition<'View> list
    /// The guards that will be executed when the route is activated.
    /// If any of them returns false, the route will not be activated.
    CanActivate: RouteGuard list
    /// The guards that will be executed when the route is deactivated.
    /// If any of them returns false, the route will not be deactivated.
    CanDeactivate: RouteGuard list
    /// The strategy that the router will use to cache the views that are rendered
    /// when the route is activated.
    CacheStrategy: CacheStrategy
  }

/// The strategy that the router will use to cache the views that are rendered
/// when the route is activated.
and [<Struct>] CacheStrategy =
  /// The Cache strategy makes that the rendered view will be stored in memory
  /// and will be re-used when the route is activated again.
  | NoCache
  /// The NoCache strategy makes that the rendered view will be re-rendered
  /// every time the route is activated.
  | Cache

and INavigate<'View> =

  abstract member Navigate:
    url: string * [<Optional>] ?cancellationToken: CancellationToken -> Task<Result<unit, NavigationError<'View>>>

  abstract member NavigateByName:
    routeName: string *
    [<Optional>] ?routeParams: IReadOnlyDictionary<string, obj> *
    [<Optional>] ?cancellationToken: CancellationToken ->
      Task<Result<unit, NavigationError<'View>>>

/// This is an object used to keep track of the routes that are defined in the application.
/// and contextual information about the route that is being activated.
/// This is used to match the URL and render the view.
/// It also contains the children routes that are defined in the application.
[<NoComparison; NoEquality>]
type RouteTrack<'View> =
  { PatternPath: string
    Definition: RouteDefinition<'View>
    ParentTrack: RouteTrack<'View> voption
    Children: RouteTrack<'View> list }

module RouteTracks =

  /// <summary>
  /// This function takes a sequence of route definitions and returns a sequence of route tracks.
  /// As required by the router to match the URL and render the view.
  /// </summary>
  [<CompiledName "FromDefinitions">]
  val fromDefinitions: routes: RouteDefinition<'View> seq -> RouteTrack<'View> list
