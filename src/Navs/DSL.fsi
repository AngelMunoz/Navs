namespace Navs

open System.Threading
open System.Threading.Tasks

[<Class>]
type Route =

  ///<summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">The view to render when the route is activated</param>
  /// <returns>A route definition</returns>
  static member inline define<'View> :
    name: string * path: string * [<InlineIfLambda>] handler: (RouteContext -> INavigable<'View> -> 'View) ->
      RouteDefinition<'View>

  /// <summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">An task returning function to render when the route is activated.</param>
  /// <returns>A route definition</returns>
  /// <remarks>A cancellation token is provided alongside the route context to allow you to support cancellation of the route activation.</remarks>
  static member inline define<'View> :
    name: string *
    path: string *
    [<InlineIfLambda>] handler: (RouteContext -> INavigable<'View> -> CancellationToken -> Task<'View>) ->
      RouteDefinition<'View>

  /// <summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">An async returning function to render when the route is activated.</param>
  /// <returns>A route definition</returns>
  /// <remarks>
  ///   A cancellation token can be extracted from Async.CancellationToken in the async workdflow
  ///   to support cancellation of the route activation.
  /// </remarks>
  static member inline define<'View> :
    name: string * path: string * [<InlineIfLambda>] handler: (RouteContext -> INavigable<'View> -> Async<'View>) ->
      RouteDefinition<'View>

module Route =
  /// <summary>
  /// This function allows you to define if the route can be restored from an in memory cache or
  /// if it should be always re-rendered when activated.
  /// </summary>
  val inline cache: strategy: CacheStrategy -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  /// <summary>
  /// A function to define if a route can be activated.
  /// </summary>
  /// <param name="guard">A function that returns a task of boolean</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canActivate:
    [<InlineIfLambda>] guard: (RouteContext voption -> RouteContext -> GuardResponse) ->
    definition: RouteDefinition<'a> ->
      RouteDefinition<'a>

  /// <summary>
  /// A Task function to define if a route can be activated.
  /// </summary>
  /// <param name="guard">A function that returns a task of boolean</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canActivateTask:
    [<InlineIfLambda>] guard: (RouteContext voption -> RouteContext -> CancellationToken -> Task<GuardResponse>) ->
    definition: RouteDefinition<'a> ->
      RouteDefinition<'a>

  /// <summary>
  /// A function to define if a route can be activated.
  /// </summary>
  /// <param name="guard">A function that returns a boolean</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canActivateAsync:
    [<InlineIfLambda>] guard: (RouteContext voption -> RouteContext -> Async<GuardResponse>) ->
    definition: RouteDefinition<'a> ->
      RouteDefinition<'a>

  /// <summary>
  /// A function to define if a route can be activated.
  /// </summary>
  /// <param name="guard">A function that returns a task of boolean</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canDeactivate:
    [<InlineIfLambda>] guard: (RouteContext voption -> RouteContext -> GuardResponse) ->
    definition: RouteDefinition<'a> ->
      RouteDefinition<'a>

  /// <summary>
  /// A Task function to define if a route can be deactivated.
  /// </summary>
  /// <param name="guard">A function that returns a task of boolean.</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canDeactivateTask:
    [<InlineIfLambda>] guard: (RouteContext voption -> RouteContext -> CancellationToken -> Task<GuardResponse>) ->
    definition: RouteDefinition<'a> ->
      RouteDefinition<'a>

  /// <summary>
  /// A Task function to define if a route can be deactivated.
  /// </summary>
  /// <param name="guard">A function that returns a  boolean</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canDeactivateAsync:
    [<InlineIfLambda>] guard: (RouteContext voption -> RouteContext -> Async<GuardResponse>) ->
    definition: RouteDefinition<'a> ->
      RouteDefinition<'a>
