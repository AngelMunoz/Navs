namespace Navs

open System
open System.Threading.Tasks
open System.Threading

type Route =

  static member inline define<'View>
    (
      name,
      path,
      [<InlineIfLambda>] view: RouteContext -> 'View
    ) =
    {
      Name = name
      Pattern = path
      GetContent = Func<_, _, _>(fun ctx _ -> Task.FromResult(view ctx))
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline define<'View>
    (
      name,
      path,
      [<InlineIfLambda>] getContent: RouteContext -> Async<'View>
    ) =
    {
      Name = name
      Pattern = path
      GetContent =
        Func<_, _, _>(fun ctx token ->
          Async.StartAsTask(getContent ctx, cancellationToken = token)
        )
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline define<'View>
    (
      name,
      path,
      [<InlineIfLambda>] getContent:
        RouteContext * CancellationToken -> Task<'View>
    ) =
    {
      Name = name
      Pattern = path
      GetContent = Func<_, _, _>(fun ctx token -> getContent(ctx, token))
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline child child definition : RouteDefinition<_> = {
    definition with
        Children = child :: definition.Children
  }

  static member inline children children definition : RouteDefinition<_> = {
    definition with
        Children = children @ definition.Children
  }

  static member inline cache strategy definition : RouteDefinition<_> = {
    definition with
        CacheStrategy = strategy
  }

module Route =
  let inline canActivateTask
    ([<InlineIfLambda>] guard: RouteContext * CancellationToken -> Task<bool>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          CanActivate =
            Func<_, _, _>(fun ctx token -> guard(ctx, token))
            :: definition.CanActivate
    }

  let inline canActivate
    ([<InlineIfLambda>] guard: RouteContext -> Async<bool>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          CanActivate =
            Func<_, _, _>(fun ctx token ->
              Async.StartAsTask(guard ctx, cancellationToken = token)
            )
            :: definition.CanActivate
    }

  let inline canDeactivateTask
    ([<InlineIfLambda>] guard: RouteContext * CancellationToken -> Task<bool>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          CanDeactivate =
            Func<_, _, _>(fun ctx token -> guard(ctx, token))
            :: definition.CanDeactivate
    }

  let inline canDeactivate
    ([<InlineIfLambda>] guard: RouteContext -> Async<bool>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          CanDeactivate =
            Func<_, _, _>(fun ctx token ->
              Async.StartAsTask(guard ctx, cancellationToken = token)
            )
            :: definition.CanDeactivate
    }
