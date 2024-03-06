namespace Navs.FuncUI

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks

open Avalonia.FuncUI
open Avalonia.FuncUI.Types

open Navs
open Navs.Router

type FuncUIRouter =
  new:
    routes: RouteDefinition<IView> seq *
    [<Optional>] ?splash: Func<IView> *
    [<Optional>] ?notFound: Func<IView> *
    [<Optional>] ?historyManager: IHistoryManager<RouteTrack<IView>> ->
      FuncUIRouter

  inherit Router<IView>

[<Class; Extension>]
type IComponentContexExtensions =
  [<Extension>]
  static member inline useRouter: context: IComponentContext * router: FuncUIRouter -> IReadable<IView>

[<Class>]
type Route =
  static member define: name: string * path: string * handler: (RouteContext -> Async<#IView>) -> RouteDefinition<IView>

  static member define:
    name: string * path: string * handler: (RouteContext * CancellationToken -> Task<#IView>) -> RouteDefinition<IView>

  static member define: name: string * path: string * handler: (RouteContext -> #IView) -> RouteDefinition<IView>
