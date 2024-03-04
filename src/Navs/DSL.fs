namespace Navs

open System.Threading.Tasks
open IcedTasks

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
      GetContent = fun ctx -> cancellableValueTask { return view ctx }
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline define<'View>
    (
      name,
      path,
      [<InlineIfLambda>] getContent: RouteContext -> CancellableValueTask<'View>
    ) =
    {
      Name = name
      Pattern = path
      GetContent = fun ctx -> getContent ctx
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline define<'View>
    (
      name,
      path,
      [<InlineIfLambda>] getContent: RouteContext -> Task<'View>
    ) =
    {
      Name = name
      Pattern = path
      GetContent = fun a -> cancellableValueTask { return! getContent a }
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
      GetContent = fun a -> cancellableValueTask { return! getContent a }
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
