namespace Navs.FuncUI

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks

open FSharp.Data.Adaptive

open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types

open Navs
open Navs.Router

type FuncUIRouter
  (
    routes: RouteDefinition<IView> seq,
    [<Optional>] ?splash: Func<INavigate<IView>, IView>,
    [<Optional>] ?notFound: Func<INavigate<IView>, IView>,
    [<Optional>] ?historyManager: IHistoryManager<RouteTrack<IView>>
  ) =
  inherit
    Router<IView>(
      RouteTracks.fromDefinitions routes,
      ?splash = splash,
      ?notFound = notFound,
      ?historyManager = historyManager
    )


[<Extension>]
type IComponentContexExtensions =

  [<Extension>]
  static member inline useRouter
    (
      context: IComponentContext,
      router: FuncUIRouter
    ) =
    let view =
      context.useStateLazy(fun () ->
        router.AdaptiveContent
        |> AVal.force
        |> Option.defaultWith(fun _ -> TextBlock.create [])
      )

    context.useEffect(
      handler = (fun () -> router.Content.Subscribe(view.Set)),
      triggers = [ EffectTrigger.AfterInit ]
    )

    view :> IReadable<IView>

type Route =

  static member define
    (
      name,
      path,
      handler: RouteContext * INavigate<IView> -> Async<#IView>
    ) : RouteDefinition<IView> =
    Navs.Route.define<IView>(
      name,
      path,
      fun args -> async {
        let! view = handler args
        return view :> IView
      }
    )

  static member define
    (
      name,
      path,
      handler:
        RouteContext * INavigate<IView> * CancellationToken -> Task<#IView>
    ) : RouteDefinition<IView> =
    Navs.Route.define<IView>(
      name,
      path,
      fun args -> task {
        let! view = handler args
        return view :> IView
      }
    )

  static member define
    (
      name,
      path,
      handler: RouteContext * INavigate<IView> -> #IView
    ) : RouteDefinition<IView> =
    Navs.Route.define<IView>(name, path, (fun args -> handler args :> IView))
