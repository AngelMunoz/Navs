namespace Navs.Avalonia

open System
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks

open Avalonia.Controls

open Navs
open Navs.Router

type AvaloniaRouter =
  new:
    routes: RouteDefinition<Control> seq *
    [<Optional>] ?splash: Func<Control> *
    [<Optional>] ?notFound: Func<Control> *
    [<Optional>] ?historyManager: IHistoryManager<RouteTrack<Control>> ->
      AvaloniaRouter

  inherit Router<Control>

[<Class>]
type Route =
  static member inline define:
    name: string * path: string * [<InlineIfLambda>] c: (RouteContext -> Async<#Control>) -> RouteDefinition<Control>

  static member inline define:
    name: string * path: string * [<InlineIfLambda>] c: (RouteContext * CancellationToken -> Task<#Control>) ->
      RouteDefinition<Control>

  static member inline define:
    name: string * path: string * [<InlineIfLambda>] c: (RouteContext -> #Control) -> RouteDefinition<Control>

module Interop =

  open System

  [<Class>]
  type Route =
    [<CompiledName "Define">]
    static member inline define:
      name: string * path: string * handler: Func<RouteContext, #Control> -> RouteDefinition<Control>

    [<CompiledName "Define">]
    static member inline define:
      name: string * path: string * handler: Func<RouteContext, CancellationToken, Task<#Control>> ->
        RouteDefinition<Control>
