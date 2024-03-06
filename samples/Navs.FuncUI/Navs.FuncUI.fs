namespace Navs.FuncUI

open System.Threading
open System.Threading.Tasks

open FSharp.Data.Adaptive

open Avalonia.Controls

open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types

open Navs
open Navs.Router
open System.Runtime.CompilerServices

type FuncUIRouter(routes: RouteDefinition<IView> seq) =
  inherit Router<IView>(RouteTracks.fromDefinitions routes)


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
      triggers = [EffectTrigger.AfterInit]
    )

    view :> IReadable<IView>

type Route =

  static member define
    (
      name,
      path,
      handler: RouteContext -> Async<#IView>
    ) : RouteDefinition<IView> =
    Navs.Route.define<IView>(
      name,
      path,
      fun ctx -> async {
        let! view = handler ctx
        return view :> IView
      }
    )

  static member define
    (
      name,
      path,
      handler: RouteContext * CancellationToken -> Task<#IView>
    ) : RouteDefinition<IView> =
    Navs.Route.define<IView>(
      name,
      path,
      fun (ctx, token) -> task {
        let! view = handler(ctx, token)
        return view :> IView
      }
    )

  static member define
    (
      name,
      path,
      handler: RouteContext -> #IView
    ) : RouteDefinition<IView> =
    Navs.Route.define<IView>(name, path, (fun ctx -> handler ctx :> IView))
