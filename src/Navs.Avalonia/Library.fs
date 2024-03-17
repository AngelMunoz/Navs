namespace Navs.Avalonia

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

open Avalonia.Controls

open FSharp.Data.Adaptive

open Navs
open Navs.Router
open Avalonia
open Avalonia.Animation
open Avalonia.Data

// enable extensions for VB.NE
[<assembly: Extension>]
do ()

[<RequireQualifiedAccess>]
module AVal =
  open Avalonia.Data

  let inline getValue (adaptiveValue: aval<_>) = AVal.force adaptiveValue

  let inline setValue (adaptiveValue: cval<_>) value =
    transact(fun () -> adaptiveValue.Value <- value)

  let inline mapSet (adaptiveValue: cval<_>) setValue =
    transact(fun () -> adaptiveValue.Value <- setValue(adaptiveValue.Value))

  let useState<'Value> (initialValue: 'Value) =
    let v = cval initialValue

    let update mapValue =
      transact(fun () -> v.Value <- mapValue(v.Value))

    v :> aval<_>, update

  [<CompiledName "ToBinding">]
  let toBinding<'Value> (value: aval<'Value>) =

    { new IBinding with
        member _.Initiate
          (
            target: Avalonia.AvaloniaObject,
            targetProperty: Avalonia.AvaloniaProperty,
            anchor: obj,
            enableDataValidation: bool
          ) : InstancedBinding =

          InstancedBinding.OneWay(
            { new IObservable<obj> with
                member _.Subscribe(observer) =
                  value.AddCallback(observer.OnNext)
            },
            BindingPriority.LocalValue
          )
    }

  module Interop =

    let UseState<'Value> (initialValue: 'Value) =
      let value = cval initialValue

      let action =
        Action<Func<'Value, 'Value>>(fun v ->
          transact(fun () -> value.Value <- v.Invoke(value.Value))
        )

      struct (value :> aval<_>, action)

[<Extension; Class>]
type AValExtensions =

  [<CompiledName "GetValue"; Extension>]
  static member inline getValue(adaptiveValue: aval<_>) =
    AVal.force adaptiveValue

  [<CompiledName "SetValue"; Extension>]
  static member inline setValue(adaptiveValue: cval<_>, value) =
    transact(fun () -> adaptiveValue.Value <- value)

  [<CompiledName "SetValue"; Extension>]
  static member inline setValue(adaptiveValue: cval<_>, setValue: Func<_, _>) =
    transact(fun () ->
      adaptiveValue.Value <- setValue.Invoke(adaptiveValue.Value)
    )

  [<CompiledName "ToBinding"; Extension>]
  static member inline toBinding(value: aval<_>) = AVal.toBinding value

type AvaloniaRouter(routes, [<Optional>] ?splash: Func<Control>) =
  let router =
    let splash = splash |> Option.map(fun f -> fun () -> f.Invoke())
    Router.get<Control>(routes, ?splash = splash)


  interface IRouter<Control> with

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

[<Class; Sealed>]
type Route =

  static member define
    (name, path, handler: RouteContext -> INavigable<Control> -> Async<#Control>) : RouteDefinition<
                                                                                      Control
                                                                                     >
    =
    Navs.Route.define<Control>(
      name,
      path,
      fun c n -> async {
        let! result = handler c n
        return result :> Control
      }
    )

  static member define
    (
      name,
      path,
      handler:
        RouteContext
          -> INavigable<Control>
          -> CancellationToken
          -> Task<#Control>
    ) : RouteDefinition<Control> =
    Navs.Route.define(
      name,
      path,
      fun c n t -> task {
        let! value = handler c n t
        return value :> Control
      }
    )

  static member define
    (name, path, handler: RouteContext -> INavigable<Control> -> #Control) : RouteDefinition<
                                                                               Control
                                                                              >
    =
    Navs.Route.define<Control>(name, path, (fun c n -> handler c n :> Control))

module Interop =

  type Route =
    [<CompiledName "Define">]
    static member inline define
      (name, path, handler: Func<RouteContext, INavigable<Control>, #Control>) =
      Navs.Route.define(
        name,
        path,
        (fun c n -> handler.Invoke(c, n) :> Control)
      )

    [<CompiledName "Define">]
    static member inline define
      (
        name,
        path,
        handler:
          Func<
            RouteContext,
            INavigable<Control>,
            CancellationToken,
            Task<#Control>
           >
      ) =
      Navs.Route.define(
        name,
        path,
        (fun c n t -> task {
          let! value = handler.Invoke(c, n, t)
          return value :> Control
        })
      )

type RouterOutlet() as this =
  inherit UserControl()

  let router = ref Unchecked.defaultof<IRouter<_>>
  let pageTransition = ref Unchecked.defaultof<IPageTransition>
  let noContent = ref Unchecked.defaultof<Control>

  let setupContent () =

    match router.Value :> obj with
    | null -> this.Content <- null
    | _ ->
      let noContent =
        match noContent.Value with
        | null -> TextBlock(Text = "No Content") :> Control
        | value -> value

      let content =
        router.Value.Content
        |> AVal.map(
          function
          | ValueSome value ->
            printfn "Value: %A" value
            value
          | _ ->
            printfn "No Content"
            noContent
        )
        |> AVal.toBinding

      let pageTransition =
        match pageTransition.Value with
        | null ->
          CompositePageTransition(
            PageTransitions =
              ResizeArray(
                [
                  CrossFade(TimeSpan.FromMilliseconds(150)) :> IPageTransition
                  PageSlide(
                    TimeSpan.FromMilliseconds(300),
                    PageSlide.SlideAxis.Horizontal
                  )
                ]
              )
          )
          :> IPageTransition
        | value -> value

      let transitionContent =
        let transitionContent =
          TransitioningContentControl(PageTransition = pageTransition)

        let binding =
          TransitioningContentControl.ContentProperty
            .Bind()
            .WithMode(mode = BindingMode.OneWay)
            .WithPriority(priority = BindingPriority.LocalValue)

        transitionContent[binding] <- content
        transitionContent

      this[RouterOutlet.ContentProperty] <- transitionContent

  member this.Router
    with get (): IRouter<Control> = router.Value
    and set (value: IRouter<Control>) =
      this.SetAndRaise(RouterOutlet.RouterProperty, router, value) |> ignore
      setupContent()

  member this.PageTransition
    with get () = pageTransition.Value
    and set (value: IPageTransition) =
      this.SetAndRaise(
        RouterOutlet.PageTransitionProperty,
        pageTransition,
        value
      )
      |> ignore

  member this.NoContent
    with get () = noContent.Value
    and set (value: Control) =
      this.SetAndRaise(RouterOutlet.NoContentProperty, noContent, value)
      |> ignore

  static member RouterProperty =
    AvaloniaProperty.RegisterDirect<RouterOutlet, IRouter<Control>>(
      "Router",
      (fun o -> o.Router),
      (fun o v -> o.Router <- v)
    )

  static member PageTransitionProperty =
    AvaloniaProperty.RegisterDirect<RouterOutlet, IPageTransition>(
      "PageTransition",
      (fun o -> o.PageTransition),
      (fun o v -> o.PageTransition <- v)
    )

  static member NoContentProperty =
    AvaloniaProperty.RegisterDirect<RouterOutlet, Control>(
      "NoContent",
      (fun o -> o.NoContent),
      (fun o v -> o.NoContent <- v)
    )

[<Extension>]
type RouterOutletExtensions =

  [<Extension>]
  static member RouterOutlet() = RouterOutlet()

  [<Extension; CompiledName "Router">]
  static member inline router
    (routerOutlet: RouterOutlet, router: IRouter<Control>)
    =
    routerOutlet[RouterOutlet.RouterProperty] <- router
    routerOutlet

  [<Extension; CompiledName "PageTransition">]

  static member inline pageTransition
    (routerOutlet: RouterOutlet, pageTransition: IPageTransition)
    =
    routerOutlet[RouterOutlet.PageTransitionProperty] <- pageTransition
    routerOutlet

  [<Extension; CompiledName "PageTransition">]
  static member inline pageTransition
    (
      routerOutlet: RouterOutlet,
      pageTransition: aval<IPageTransition>,
      [<Optional>] ?mode: BindingMode,
      [<Optional>] ?priority: BindingPriority
    ) =
    let mode = defaultArg mode BindingMode.OneWay
    let priority = defaultArg priority BindingPriority.LocalValue

    let descriptor =
      RouterOutlet.PageTransitionProperty
        .Bind()
        .WithMode(mode = mode)
        .WithPriority(priority = priority)

    routerOutlet[descriptor] <- AVal.toBinding pageTransition
    routerOutlet

  [<Extension; CompiledName "NoContent">]
  static member inline noContent
    (routerOutlet: RouterOutlet, noContent: Control)
    =
    routerOutlet[RouterOutlet.NoContentProperty] <- noContent
    routerOutlet

  [<Extension; CompiledName "NoContent">]
  static member inline noContent
    (
      routerOutlet: RouterOutlet,
      noContent: aval<Control>,
      [<Optional>] ?mode: BindingMode,
      [<Optional>] ?priority: BindingPriority
    ) =
    let mode = defaultArg mode BindingMode.OneWay
    let priority = defaultArg priority BindingPriority.LocalValue

    let descriptor =
      RouterOutlet.NoContentProperty
        .Bind()
        .WithMode(mode = mode)
        .WithPriority(priority = priority)

    routerOutlet[descriptor] <- AVal.toBinding noContent
    routerOutlet
