namespace Navs

open System.Threading.Tasks
open IcedTasks

type Route =

  static member inline define(name, path, view) = {
    Name = name
    Pattern = path
    GetContent = Content view
    Children = []
    CanActivate = []
    CanDeactivate = []
    CacheStrategy = Cache
  }

  static member inline defineResolve
    (
      name,
      path,
      [<InlineIfLambda>] getContent: RouteContext -> CancellableValueTask<_>
    ) =
    {
      Name = name
      Pattern = path
      GetContent = Resolve getContent
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline defineResolve
    (
      name,
      path,
      [<InlineIfLambda>] getContent: RouteContext -> Task<_>
    ) =
    {
      Name = name
      Pattern = path
      GetContent =
        Resolve(fun a -> cancellableValueTask { return! getContent a })
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline defineResolve
    (
      name,
      path,
      [<InlineIfLambda>] getContent: RouteContext -> Async<_>
    ) =
    {
      Name = name
      Pattern = path
      GetContent =
        Resolve(fun a -> cancellableValueTask { return! getContent a })
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

  static member inline canActivate
    ([<InlineIfLambda>] guard)
    definition
    : RouteDefinition<_> =
    {
      definition with
          CanActivate = guard :: definition.CanActivate
    }

  static member inline canDeactivate
    ([<InlineIfLambda>] guard)
    definition
    : RouteDefinition<_> =
    {
      definition with
          CanDeactivate = guard :: definition.CanDeactivate
    }
