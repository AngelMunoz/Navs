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
    [<Optional>] ?splash: Func<INavigate<Control>, Control>,
    [<Optional>] ?notFound: Func<INavigate<Control>, Control>,
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

  static member define
    (
      name,
      path,
      handler: RouteContext * INavigate<Control> -> Async<#Control>
    ) : RouteDefinition<Control> =
    Navs.Route.define<Control>(
      name,
      path,
      fun args -> async {
        let! result = handler args
        return result :> Control
      }
    )

  static member define
    (
      name,
      path,
      handler:
        RouteContext * INavigate<Control> * CancellationToken -> Task<#Control>
    ) : RouteDefinition<Control> =
    Navs.Route.define(
      name,
      path,
      fun args -> task {
        let! result = handler args
        return result :> Control
      }
    )

  static member define
    (
      name,
      path,
      handler: RouteContext * INavigate<Control> -> #Control
    ) : RouteDefinition<Control> =
    Navs.Route.define<Control>(
      name,
      path,
      fun args -> handler args :> Control
    )

module Interop =

  type Route =
    [<CompiledName "Define">]
    static member inline define
      (
        name,
        path,
        handler: Func<RouteContext, INavigate<Control>, #Control>
      ) =
      Navs.Route.define(name, path, (fun ctx -> handler.Invoke ctx :> Control))

    [<CompiledName "Define">]
    static member inline define
      (
        name,
        path,
        handler:
          Func<RouteContext, INavigate<Control>, CancellationToken, Task<#Control>>
      ) =
      Navs.Route.define(
        name,
        path,
        (fun args -> task {
          let! value = handler.Invoke args
          return value :> Control
        })
      )
