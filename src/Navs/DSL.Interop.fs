namespace Navs.Interop

open System
open System.Threading.Tasks
open System.Runtime.CompilerServices

open Navs

// make sure extensions are visible in VB.NET
[<assembly: Extension>]
do ()

type Route =

  static member inline Define<'View>
    (
      name,
      path,
      getContent: Func<RouteContext, 'View>
    ) =
    {
      Name = name
      Pattern = path
      GetContent =
        Func<_, _, _, _>(fun ctx _ _ -> Task.FromResult(getContent.Invoke(ctx)))
      Children = []
      CanActivate = []
      CanDeactivate = []
      CacheStrategy = Cache
    }

  static member inline Define<'View>(name, path, getContent: GetView<'View>) = {
    Name = name
    Pattern = path
    GetContent = getContent
    Children = []
    CanActivate = []
    CanDeactivate = []
    CacheStrategy = Cache
  }

[<Extension>]
type RouteDefinitionExtensions =


  [<Extension>]
  static member inline Child<'View>
    (
      routeDef: RouteDefinition<'View>,
      child: RouteDefinition<'View>
    ) =
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
      [<ParamArray>] guards: RouteGuard array
    ) =
    {
      routeDef with
          CanActivate = [ yield! routeDef.CanActivate; yield! guards ]
    }

  [<Extension>]
  static member inline CanDeactivate<'View>
    (
      routeDef: RouteDefinition<'View>,
      [<ParamArray>] guards: RouteGuard array
    ) =
    {
      routeDef with
          CanDeactivate = [ yield! routeDef.CanDeactivate; yield! guards ]
    }

  [<Extension>]
  static member inline CacheOnVisit<'View>(routeDef: RouteDefinition<'View>) = {
    routeDef with
        CacheStrategy = CacheStrategy.Cache
  }

  [<Extension>]
  static member inline NoCacheOnVisit<'View>(routeDef: RouteDefinition<'View>) = {
    routeDef with
        CacheStrategy = CacheStrategy.NoCache
  }
