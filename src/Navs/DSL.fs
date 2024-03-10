namespace Navs

open System
open System.Threading.Tasks
open System.Threading

type Route =

  static member inline define<'View>
    (
      name,
      path,
      [<InlineIfLambda>] handler: RouteContext * INavigate<'View> -> 'View
    ) =
    {
      Name = name
      Pattern = path
      GetContent =
        Func<_, _, _, _>(fun ctx nav _ -> Task.FromResult(handler(ctx, nav)))
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline define<'View>
    (
      name,
      path,
      [<InlineIfLambda>] handler:
        RouteContext * INavigate<'View> -> Async<'View>
    ) =
    {
      Name = name
      Pattern = path
      GetContent =
        Func<_, _, _, _>(fun ctx nav token ->
          Async.StartImmediateAsTask(
            handler(ctx, nav),
            cancellationToken = token
          )
        )
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline define<'View>
    (
      name,
      path: string,
      [<InlineIfLambda>] handler:
        RouteContext * INavigate<'View> * CancellationToken -> Task<'View>
    ) =
    {
      Name = name
      Pattern = path
      GetContent =
        Func<_, _, _, _>(fun ctx nav token -> handler(ctx, nav, token))
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline child child definition : RouteDefinition<_> = {
    definition with
        Children =
          {
            child with
                Pattern =
                  if child.Pattern.StartsWith('/') then
                    child.Pattern[1..]
                  else
                    child.Pattern
          }
          :: definition.Children
  }

  static member inline children children definition : RouteDefinition<_> = {
    definition with
        Children = [
          yield! children
          for child in definition.Children ->
            {
              child with
                  Pattern =
                    if child.Pattern.StartsWith('/') then
                      child.Pattern[1..]
                    else
                      child.Pattern
            }
        ]
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
              Async.StartImmediateAsTask(guard ctx, cancellationToken = token)
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
              Async.StartImmediateAsTask(guard ctx, cancellationToken = token)
            )
            :: definition.CanDeactivate
    }
