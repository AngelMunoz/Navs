namespace Navs

open System
open System.Threading.Tasks
open System.Threading

type Route =

  static member inline define<'View>
    (
      name,
      path,
      [<InlineIfLambda>] handler: RouteContext -> INavigable<'View> -> 'View
    ) =
    {
      name = name
      pattern = path
      getContent = fun ctx nav _ -> task { return handler ctx nav }
      children = []
      canActivate = []
      canDeactivate = []
      cacheStrategy = Cache
    }

  static member inline define<'View>
    (
      name,
      path,
      [<InlineIfLambda>] handler:
        RouteContext -> INavigable<'View> -> Async<'View>
    ) =
    {
      name = name
      pattern = path
      getContent =
        fun ctx nav token ->
          Async.StartImmediateAsTask(handler ctx nav, cancellationToken = token)

      children = []
      canActivate = []
      canDeactivate = []
      cacheStrategy = Cache
    }

  static member inline define<'View>
    (
      name,
      path: string,
      [<InlineIfLambda>] handler:
        RouteContext -> INavigable<'View> -> CancellationToken -> Task<'View>
    ) =
    {
      name = name
      pattern = path
      getContent = handler
      children = []
      canActivate = []
      canDeactivate = []
      cacheStrategy = Cache
    }

  static member inline child child definition : RouteDefinition<_> = {
    definition with
        children =
          {
            child with
                pattern =
                  if child.pattern.StartsWith('/') then
                    child.pattern[1..]
                  else
                    child.pattern
          }
          :: definition.children
  }

  static member inline children children definition : RouteDefinition<_> = {
    definition with
        children = [
          yield! children
          for child in definition.children ->
            {
              child with
                  pattern =
                    if child.pattern.StartsWith('/') then
                      child.pattern[1..]
                    else
                      child.pattern
            }
        ]
  }

  static member inline cache strategy definition : RouteDefinition<_> = {
    definition with
        cacheStrategy = strategy
  }

module Route =
  let inline canActivateTask
    ([<InlineIfLambda>] guard:
      RouteContext -> INavigable<_> -> CancellationToken -> Task<GuardResponse>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          canActivate = guard :: definition.canActivate
    }

  let inline canActivate
    ([<InlineIfLambda>] guard:
      RouteContext -> INavigable<_> -> Async<GuardResponse>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          canActivate =
            (fun ctx nav token ->
              Async.StartImmediateAsTask(
                guard ctx nav,
                cancellationToken = token
              )
            )
            :: definition.canActivate
    }

  let inline canDeactivateTask
    ([<InlineIfLambda>] guard:
      RouteContext -> INavigable<_> -> CancellationToken -> Task<GuardResponse>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          canDeactivate = guard :: definition.canDeactivate
    }

  let inline canDeactivate
    ([<InlineIfLambda>] guard:
      RouteContext -> INavigable<_> -> Async<GuardResponse>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          canDeactivate =
            (fun ctx nav token ->
              Async.StartImmediateAsTask(
                guard ctx nav,
                cancellationToken = token
              )
            )
            :: definition.canDeactivate
    }
