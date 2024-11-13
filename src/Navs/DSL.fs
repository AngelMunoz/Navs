namespace Navs

open System
open System.Threading.Tasks
open System.Threading
open IcedTasks

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
      getContent =
        GetView<'View>(fun ctx nav -> cancellableValueTask {
          return handler ctx nav
        })
      canActivate = []
      canDeactivate = []
      cacheStrategy = Cache
    }

  static member inline define<'View>
    (
      name,
      path,
      [<InlineIfLambda>] handler:
        RouteContext -> INavigable<'View> -> CancellationToken -> Task<'View>
    ) =
    {
      name = name
      pattern = path
      getContent =
        GetView<'View>(fun ctx nav -> cancellableValueTask {
          let! token = CancellableValueTask.getCancellationToken()
          return! handler ctx nav token
        })
      canActivate = []
      canDeactivate = []
      cacheStrategy = Cache
    }

  static member inline define<'View>
    (
      name,
      path: string,
      [<InlineIfLambda>] handler:
        RouteContext -> INavigable<'View> -> Async<'View>
    ) =
    {
      name = name
      pattern = path
      getContent =
        GetView<'View>(fun ctx nav -> cancellableValueTask {
          return! handler ctx nav
        })
      canActivate = []
      canDeactivate = []
      cacheStrategy = Cache
    }


module Route =

  let inline cache strategy definition : RouteDefinition<_> = {
    definition with
        cacheStrategy = strategy
  }

  let inline canActivate
    ([<InlineIfLambda>] guard:
      RouteContext voption -> RouteContext -> GuardResponse)
    definition
    =
    {
      definition with
          canActivate =
            RouteGuard<'View>(fun activeCtx nextCtx -> cancellableValueTask {
              return guard activeCtx nextCtx
            })
            :: definition.canActivate
    }

  let inline canActivateTask
    ([<InlineIfLambda>] guard:
      RouteContext voption
        -> RouteContext
        -> CancellationToken
        -> Task<GuardResponse>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          canActivate =
            RouteGuard<'View>(fun activeCtx nextCtx -> cancellableValueTask {
              let! token = CancellableValueTask.getCancellationToken()
              return! guard activeCtx nextCtx token
            })
            :: definition.canActivate
    }

  let inline canActivateAsync
    ([<InlineIfLambda>] guard:
      RouteContext voption -> RouteContext -> Async<GuardResponse>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          canActivate =
            RouteGuard<'View>(fun activeCtx nextCtx -> cancellableValueTask {
              return! guard activeCtx nextCtx
            })
            :: definition.canActivate
    }

  let inline canDeactivate
    ([<InlineIfLambda>] guard:
      RouteContext voption -> RouteContext -> GuardResponse)
    definition
    =
    {
      definition with
          canDeactivate =
            RouteGuard<'View>(fun activeCtx nextCtx -> cancellableValueTask {
              return guard activeCtx nextCtx
            })
            :: definition.canDeactivate
    }


  let inline canDeactivateTask
    ([<InlineIfLambda>] guard:
      RouteContext voption
        -> RouteContext
        -> CancellationToken
        -> Task<GuardResponse>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          canDeactivate =
            RouteGuard<'View>(fun activeCtx nextCtx -> cancellableValueTask {
              let! token = CancellableValueTask.getCancellationToken()
              return! guard activeCtx nextCtx token
            })
            :: definition.canDeactivate
    }

  let inline canDeactivateAsync
    ([<InlineIfLambda>] guard:
      RouteContext voption -> RouteContext -> Async<GuardResponse>)
    definition
    : RouteDefinition<_> =
    {
      definition with
          canDeactivate =
            RouteGuard<'View>(fun activeCtx nextCtx -> cancellableValueTask {
              return! guard activeCtx nextCtx
            })
            :: definition.canDeactivate
    }
