namespace Navs

open System.Threading
open System.Threading.Tasks

type SyncView<'View> = RouteContext -> INavigable<'View> -> 'View
type AsyncView<'View> = RouteContext -> INavigable<'View> -> Async<'View>
type TaskView<'View> = RouteContext -> INavigable<'View> -> CancellationToken -> Task<'View>
type SyncGuard = RouteContext voption -> RouteContext -> GuardResponse
type AsyncGuard = RouteContext voption -> RouteContext -> Async<GuardResponse>
type TaskGuard = RouteContext voption -> RouteContext -> CancellationToken -> Task<GuardResponse>

[<Class>]
type Route =

  ///<summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">The view to render when the route is activated</param>
  /// <returns>A route definition</returns>
  static member inline define<'View> :
    name: string * path: string * [<InlineIfLambda>] handler: SyncView<'View> -> RouteDefinition<'View>

  /// <summary>Defines a route in the application</summary>
  /// <param name="name">The name of the route</param>
  /// <param name="path">A templated URL that will be used to match this route</param>
  /// <param name="handler">An task returning function to render when the route is activated.</param>
  /// <returns>A route definition</returns>
  /// <remarks>A cancellation token is provided alongside the route context to allow you to support cancellation of the route activation.</remarks>
  static member inline define<'View> :
    name: string * path: string * [<InlineIfLambda>] handler: TaskView<'View> -> RouteDefinition<'View>

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
    name: string * path: string * [<InlineIfLambda>] handler: AsyncView<'View> -> RouteDefinition<'View>

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
  val inline canActivate: [<InlineIfLambda>] guard: SyncGuard -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  /// <summary>
  /// A Task function to define if a route can be activated.
  /// </summary>
  /// <param name="guard">A function that returns a task of boolean</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canActivateTask:
    [<InlineIfLambda>] guard: TaskGuard -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  /// <summary>
  /// A function to define if a route can be activated.
  /// </summary>
  /// <param name="guard">A function that returns a boolean</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canActivateAsync:
    [<InlineIfLambda>] guard: AsyncGuard -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  /// <summary>
  /// A function to define if a route can be activated.
  /// </summary>
  /// <param name="guard">A function that returns a task of boolean</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canDeactivate:
    [<InlineIfLambda>] guard: SyncGuard -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  /// <summary>
  /// A Task function to define if a route can be deactivated.
  /// </summary>
  /// <param name="guard">A function that returns a task of boolean.</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canDeactivateTask:
    [<InlineIfLambda>] guard: TaskGuard -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  /// <summary>
  /// A Task function to define if a route can be deactivated.
  /// </summary>
  /// <param name="guard">A function that returns a  boolean</param>
  /// <param name="definition">The route definition</param>
  /// <returns>The route definition with the guard added</returns>
  val inline canDeactivateAsync:
    [<InlineIfLambda>] guard: AsyncGuard -> definition: RouteDefinition<'a> -> RouteDefinition<'a>
