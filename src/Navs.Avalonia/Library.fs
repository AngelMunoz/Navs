namespace Navs.Avalonia

open System
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks

open Avalonia.Controls

open Navs
open Navs.Router

type AvaloniaRouter
  (
    routes,
    [<Optional>] ?splash: Func<Control>,
    [<Optional>] ?notFound: Func<Control>,
    [<Optional>] ?historyManager: IHistoryManager<RouteTrack<Control>>
  ) =
  inherit
    Router<Control>(
      RouteTracks.fromDefinitions routes,
      ?splash = splash,
      ?notFound = notFound,
      ?historyManager = historyManager
    )

type Route =

  static member define<'View when 'View :> Control>
    (
      name,
      path,
      handler: RouteContext -> Async<'View>
    ) : RouteDefinition<Control> =
    Navs.Route.define<Control>(
      name,
      path,
      fun ctx -> async {
        let! result = handler ctx
        return result :> Control
      }
    )

  static member define<'View when 'View :> Control>
    (
      name,
      path,
      handler: RouteContext * CancellationToken -> Task<'View>
    ) : RouteDefinition<Control> =
    Navs.Route.define(
      name,
      path,
      fun (ctx, token) -> task {
        let! result = handler(ctx, token)
        return result :> Control
      }
    )

  static member define<'View when 'View :> Control>
    (
      name,
      path,
      handler: RouteContext -> 'View
    ) : RouteDefinition<Control> =
    Navs.Route.define<Control>(name, path, (fun ctx -> handler ctx :> Control))

module Interop =

  type Route =
    [<CompiledName "Define">]
    static member inline define
      (
        name,
        path,
        handler: Func<RouteContext, #Control>
      ) =
      Navs.Route.define(name, path, (fun ctx -> handler.Invoke(ctx) :> Control))

    [<CompiledName "Define">]
    static member inline define
      (
        name,
        path,
        handler: Func<RouteContext, CancellationToken, Task<#Control>>
      ) =
      Navs.Route.define(
        name,
        path,
        (fun (ctx, token) -> task {
          let! value = handler.Invoke(ctx, token)
          return value :> Control
        })
      )
