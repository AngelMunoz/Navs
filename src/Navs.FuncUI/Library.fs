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
    Router.build<IView>(routes, ?splash = splash)


  interface IRouter<IView> with

    member _.State = router.State

    member _.StateSnapshot = router.StateSnapshot

    member _.Route = router.Route

    member _.RouteSnapshot = router.RouteSnapshot

    member _.Content = router.Content

    member _.ContentSnapshot = router.ContentSnapshot

    member _.Navigate(a, [<Optional>] ?b) =
      router.Navigate(a, ?cancellationToken = b)

    member _.NavigateByName(a, [<Optional>] ?b, [<Optional>] ?c) =
      router.NavigateByName(a, ?routeParams = b, ?cancellationToken = c)


type CValState<'Type>(initial: cval<'Type>) =
  let instanceId = Guid.NewGuid()

  interface IWritable<'Type> with
    member _.InstanceId = instanceId
    member _.InstanceType = InstanceType.Source

    member _.ValueType = typeof<'Type>

    member _.Current = initial.Value

    member _.Subscribe(handler: 'Type -> unit) = initial.AddCallback(handler)

    member _.SubscribeAny(handler: obj -> unit) = initial.AddCallback(handler)

    member _.Set(value: 'Type) =
      transact(fun _ -> initial.Value <- value)

    member _.Dispose() = ()


[<Extension>]
type IComponentContexExtensions =

  [<Extension>]
  static member inline useRouter
    (context: IComponentContext, router: IRouter<IView>)
    =
    let view = context.useState(router.Content |> AVal.force)

    context.useEffect(
      handler = (fun () -> router.Content.AddCallback(view.Set)),
      triggers = [ EffectTrigger.AfterInit ]
    )

    view :> IReadable<IView voption>

  [<Extension>]
  static member inline useAVal(context: IComponentContext, aVal: aval<'a>) =
    let view = context.useState(aVal |> AVal.force)

    context.useEffect(
      handler = (fun () -> aVal.AddCallback(view.Set)),
      triggers = [ EffectTrigger.AfterInit ]
    )

    view :> IReadable<'a>

  [<Extension>]
  static member useCval(context: IComponentContext, cVal: cval<'a>) =
    context.usePassed(new CValState<'a>(cVal))


type Route =

  static member define
    (name, path, handler: RouteContext -> INavigable<IView> -> Async<#IView>) : RouteDefinition<
                                                                                  IView
                                                                                 >
    =
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
    (name, path, handler: RouteContext -> INavigable<IView> -> #IView) : RouteDefinition<
                                                                           IView
                                                                          >
    =
    Navs.Route.define<IView>(name, path, (fun c n -> handler c n :> IView))


[<AutoOpen>]
module DSL =
  open Avalonia.Animation
  open Avalonia.Controls

  [<Class>]
  type RouterOutlet =

    static member create
      (router: IRouter<IView>, ?noContent: IView, ?transition: IPageTransition) =
      Component.create(
        "router-outlet",
        fun ctx ->
          let view = ctx.useRouter(router)

          let content =
            view
            |> State.readMap(fun v ->
              v
              |> ValueOption.defaultValue(
                noContent |> Option.defaultValue(ContentControl.create [])
              )
            )

          let transition =
            transition
            |> Option.defaultWith(fun () ->
              CompositePageTransition(
                PageTransitions =
                  ResizeArray(
                    [
                      CrossFade(TimeSpan.FromMilliseconds(150))
                      :> IPageTransition
                      PageSlide(
                        TimeSpan.FromMilliseconds(300),
                        PageSlide.SlideAxis.Horizontal
                      )
                    ]
                  )
              )
            )

          TransitioningContentControl.create [
            TransitioningContentControl.content content.Current
            TransitioningContentControl.pageTransition transition
          ]
      )
