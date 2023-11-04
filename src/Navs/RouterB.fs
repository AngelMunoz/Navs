namespace Navs.RouterB

open System
open FSharp.Control.Reactive

open FsToolkit.ErrorHandling

open Navs
open Navs.Experiments



type Router<'View>
  (routes: RouteDefinition<'View> list, ?history: IHistoryManager<'View>) =
  let history = defaultArg history (HistoryManager())

  let view = Subject.broadcast
  let routeMap = RouteTree.ofList routes

  member val Content: IObservable<'View> = view with get
