namespace Navs.Interop

open System
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

open Navs

[<Class>]
type Route =
  static member inline Define:
    name: string * path: string * getContent: Func<RouteContext, 'View> -> RouteDefinition<'View>

  static member inline Define:
    name: string * path: string * getContent: Func<RouteContext, CancellationToken, Task<'View>> ->
      RouteDefinition<'View>

[<Class; Extension>]
type RouteDefinitionExtensions =
  [<Extension>]
  static member inline Child: routeDef: RouteDefinition<'View> * child: RouteDefinition<'View> -> RouteDefinition<'View>

  [<Extension>]
  static member inline Children:
    routeDef: RouteDefinition<'View> * [<ParamArray>] children: RouteDefinition<'View> array -> RouteDefinition<'View>

  [<Extension>]
  static member inline CanActivate:
    routeDef: RouteDefinition<'View> * [<ParamArray>] guards: RouteGuard array -> RouteDefinition<'View>

  [<Extension>]
  static member inline CanDeactivate:
    routeDef: RouteDefinition<'View> * [<ParamArray>] guards: RouteGuard array -> RouteDefinition<'View>

  [<Extension>]
  static member inline CacheOnVisit: routeDef: RouteDefinition<'View> -> RouteDefinition<'View>

  [<Extension>]
  static member inline NoCacheOnVisit: routeDef: RouteDefinition<'View> -> RouteDefinition<'View>
