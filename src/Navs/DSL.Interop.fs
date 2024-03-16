namespace Navs.Interop

open System
open System.Threading.Tasks
open System.Runtime.CompilerServices

open Navs
open System.Threading

// make sure extensions are visible in VB.NET
[<assembly: Extension>]
do ()

type Route =

  static member inline Define<'View>
    (name, path, getContent: Func<RouteContext, INavigable<'View>, 'View>) =
    {
      name = name
      pattern = path
      getContent = fun ctx nav _ -> task { return getContent.Invoke(ctx, nav) }
      children = []
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
      getContent = fun ctx nav token -> getContent.Invoke(ctx, nav, token)
      children = []
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
  static member inline Child<'View>
    (routeDef: RouteDefinition<'View>, child: RouteDefinition<'View>) =
    Route.child child routeDef

  [<Extension>]
  static member inline Children<'View>
    (
      routeDef: RouteDefinition<'View>,
      [<ParamArray>] children: RouteDefinition<'View> array
    ) =
    Route.children children routeDef

  [<Extension>]
  static member inline CanActivate<'View>
    (
      routeDef: RouteDefinition<'View>,
      [<ParamArray>] guards:
        Func<
          RouteContext,
          INavigable<'View>,
          CancellationToken,
          Task<GuardResponse>
         > array
    ) =
    {
      routeDef with
          canActivate = [
            yield! routeDef.canActivate
            for guard in guards do
              FuncConvert.FromFunc(guard)
          ]
    }

  [<Extension>]
  static member inline CanDeactivate<'View>
    (
      routeDef: RouteDefinition<'View>,
      [<ParamArray>] guards:
        Func<
          RouteContext,
          INavigable<'View>,
          CancellationToken,
          Task<GuardResponse>
         > array
    ) =
    {
      routeDef with
          canDeactivate = [
            yield! routeDef.canDeactivate
            for guard in guards do
              FuncConvert.FromFunc(guard)
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
