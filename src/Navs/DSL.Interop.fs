namespace Navs.Interop

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.CompilerServices
open IcedTasks
open Navs

// make sure extensions are visible in VB.NET
[<assembly: Extension>]
do ()

type Route =

  static member inline Define<'View>
    (name, path, getContent: Func<RouteContext, INavigable<'View>, 'View>) =
    {
      name = name
      pattern = path
      getContent = GetView<'View>(fun ctx nav -> cancellableValueTask { return getContent.Invoke(ctx, nav) })
      canActivate = []
      canDeactivate = []
      cacheStrategy = Cache
    }

  static member inline Define<'View>
    (
      name,
      path,
      getContent:
        Func<RouteContext, INavigable<'View>, CancellationToken, Task<'View>>
    ) =
    {
      name = name
      pattern = path
      getContent = GetView<'View>(fun ctx nav -> cancellableValueTask {
        let! token = CancellableValueTask.getCancellationToken()
        return! getContent.Invoke(ctx, nav, token) })
      canActivate = []
      canDeactivate = []
      cacheStrategy = Cache
    }


[<RequireQualifiedAccess>]
module Guard =
  let inline Continue () = Continue
  let inline Stop () = Stop

  let inline Redirect url = Redirect url


[<Extension>]
type RouteDefinitionExtensions =

  [<Extension>]
  static member inline CanActivate<'View>
    (
      routeDef: RouteDefinition<'View>,
      [<ParamArray>] guards:
        Func<RouteContext | null, RouteContext, GuardResponse> array
    ) =
    {
      routeDef with
          canActivate = [
            yield! routeDef.canActivate
            for guard in guards do
              RouteGuard<'View>(fun ctx nextCtx -> cancellableValueTask {return guard.Invoke(ctx |> ValueOption.defaultValue (unbox null), nextCtx)})
          ]
    }

  [<Extension>]
  static member inline CanActivate<'View>
    (
      routeDef: RouteDefinition<'View>,
      [<ParamArray>] guards:
        Func<
          RouteContext | null,
          RouteContext,
          CancellationToken,
          Task<GuardResponse>
         > array
    ) =
    {
      routeDef with
          canActivate = [
            yield! routeDef.canActivate
            for guard in guards do
              RouteGuard<'View>(fun ctx nextCtx -> cancellableValueTask {
              let! token = CancellableValueTask.getCancellationToken()
              return! guard.Invoke(ctx |> ValueOption.defaultValue (unbox null), nextCtx, token) })
          ]
    }

  [<Extension>]
  static member inline CanDeactivate<'View>
    (
      routeDef: RouteDefinition<'View>,
      [<ParamArray>] guards:
        Func<RouteContext | null, RouteContext, GuardResponse> array
    ) =
    {
      routeDef with
          canDeactivate = [
            yield! routeDef.canDeactivate
            for guard in guards do
              RouteGuard<'View>(fun ctx nextCtx -> cancellableValueTask {return guard.Invoke(ctx |> ValueOption.defaultValue (unbox null), nextCtx)})
          ]
    }
  [<Extension>]
  static member inline CanDeactivate<'View>
    (
      routeDef: RouteDefinition<'View>,
      [<ParamArray>] guards:
        Func<
          RouteContext | null,
          RouteContext,
          CancellationToken,
          Task<GuardResponse>
         > array
    ) =
    {
      routeDef with
          canDeactivate = [
            yield! routeDef.canDeactivate
            for guard in guards do
              RouteGuard<'View>(fun ctx nextCtx -> cancellableValueTask {
                  let! token = CancellableValueTask.getCancellationToken()
                  return! guard.Invoke(ctx |> ValueOption.defaultValue (unbox null), nextCtx, token) })
          ]
    }

  [<Extension>]
  static member inline CacheOnVisit<'View>(routeDef: RouteDefinition<'View>) = {
    routeDef with
        cacheStrategy = CacheStrategy.Cache
  }

  [<Extension>]
  static member inline NoCacheOnVisit<'View>(routeDef: RouteDefinition<'View>) = {
    routeDef with
        cacheStrategy = CacheStrategy.NoCache
  }
