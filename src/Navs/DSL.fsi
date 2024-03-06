namespace Navs

open System.Threading
open System.Threading.Tasks

[<Class>]
type Route =
  static member inline define<'View> :
    name: string * path: string * [<InlineIfLambda>] view: (RouteContext -> 'View) -> RouteDefinition<'View>

  static member inline define<'View> :
    name: string * path: string * [<InlineIfLambda>] getContent: (RouteContext * CancellationToken -> Task<'View>) ->
      RouteDefinition<'View>

  static member inline define<'View> :
    name: string * path: string * [<InlineIfLambda>] getContent: (RouteContext -> Async<'View>) ->
      RouteDefinition<'View>

  static member inline child: child: RouteDefinition<'a> -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  static member inline children:
    children: RouteDefinition<'a> list -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  static member inline cache: strategy: CacheStrategy -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

module Route =
  val inline canActivateTask:
    [<InlineIfLambda>] guard: (RouteContext * CancellationToken -> Task<bool>) ->
    definition: RouteDefinition<'a> ->
      RouteDefinition<'a>

  val inline canActivate:
    [<InlineIfLambda>] guard: (RouteContext -> Async<bool>) -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  val inline canDeactivateTask:
    [<InlineIfLambda>] guard: (RouteContext * CancellationToken -> Task<bool>) ->
    definition: RouteDefinition<'a> ->
      RouteDefinition<'a>

  val inline canDeactivate:
    [<InlineIfLambda>] guard: (RouteContext -> Async<bool>) -> definition: RouteDefinition<'a> -> RouteDefinition<'a>
