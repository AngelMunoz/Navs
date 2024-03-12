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
  (routes: RouteDefinition<IView> seq, [<Optional>] ?splash: Func<IView>) =

  let router =
    let splash = splash |> Option.map(fun f -> fun () -> f.Invoke())
    Router.get<IView>(routes, ?splash = splash)


  interface IRouter<IView> with
    member _.Content = router.Content

    member _.Navigate(a, [<Optional>] ?b) =
      router.Navigate(a, ?cancellationToken = b)

    member _.NavigateByName(a, [<Optional>] ?b, [<Optional>] ?c) =
      router.NavigateByName(a, ?routeParams = b, ?cancellationToken = c)

[<Extension>]
type IComponentContexExtensions =

  [<Extension>]
  static member inline useRouter
    (
      context: IComponentContext,
      router: IRouter<IView>
    ) =
    let view =
      context.useStateLazy(fun () ->
        router.Content
        |> AVal.force
        |> ValueOption.defaultWith(fun _ -> TextBlock.create [])
      )

    context.useEffect(
      handler =
        (fun () ->
          router.Content.AddCallback(
            function
            | ValueSome v -> view.Set v
            | ValueNone -> ()
          )
        ),
      triggers = [ EffectTrigger.AfterInit ]
    )

    view :> IReadable<IView>

type Route =

  static member define
    (
      name,
      path,
      handler: RouteContext -> INavigable<IView> -> Async<#IView>
    ) : RouteDefinition<IView> =
    Navs.Route.define<IView>(
      name,
      path,
      fun c n -> async {
        let! view = handler c n
        return view :> IView
      }
    )

  static member define
    (
      name,
      path,
      handler:
        RouteContext -> INavigable<IView> -> CancellationToken -> Task<#IView>
    ) : RouteDefinition<IView> =
    Navs.Route.define<IView>(
      name,
      path,
      fun c n t -> task {
        let! view = handler c n t
        return view :> IView
      }
    )

  static member define
    (
      name,
      path,
      handler: RouteContext -> INavigable<IView> -> #IView
    ) : RouteDefinition<IView> =
    Navs.Route.define<IView>(name, path, (fun c n -> handler c n :> IView))
