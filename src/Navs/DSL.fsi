namespace Navs

open System.Threading.Tasks

[<Class>]
type Route =
  static member inline define:
    name: string * path: string * [<InlineIfLambda>] view: (RouteContext -> 'View) -> RouteDefinition<'View>

  static member inline define:
    name: string *
    path: string *
    [<InlineIfLambda>] getContent: (RouteContext -> System.Threading.CancellationToken -> ValueTask<'View>) ->
      RouteDefinition<'View>

  static member inline define:
    name: string * path: string * [<InlineIfLambda>] getContent: (RouteContext -> Task<'View>) -> RouteDefinition<'View>

  static member inline define:
    name: string * path: string * [<InlineIfLambda>] getContent: (RouteContext -> Async<'View>) ->
      RouteDefinition<'View>

  static member inline child: child: RouteDefinition<'a> -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  static member inline children:
    children: RouteDefinition<'a> list -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  static member inline canActivate:
    [<InlineIfLambda>] guard: RouteGuard -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  static member inline canDeactivate:
    [<InlineIfLambda>] guard: RouteGuard -> definition: RouteDefinition<'a> -> RouteDefinition<'a>

  static member inline cache: strategy: CacheStrategy -> definition: RouteDefinition<'a> -> RouteDefinition<'a>
