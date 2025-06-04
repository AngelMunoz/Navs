#nowarn "57"

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
open Microsoft.Extensions.Logging

// enable extensions for VB.NE
[<assembly: Extension>]
do ()

[<RequireQualifiedAccess>]
module AVal =

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

  [<CompiledName "ToObservable">]
  let toObservable (value: aval<_>) =
    { new IObservable<_> with
        member _.Subscribe(observer) = value.AddCallback(observer.OnNext)
    }

  [<CompiledName "ToBinding">]
  let toBinding<'Value> (value: aval<'Value>) =
    (value |> toObservable).ToBinding()

  module Interop =

    let UseState<'Value> (initialValue: 'Value) =
      let value = cval initialValue

      let action =
        Action<Func<'Value, 'Value>>(fun v ->
          transact(fun () -> value.Value <- v.Invoke(value.Value))
        )

      struct (value :> aval<_>, action)

[<RequireQualifiedAccess>]
module CVal =

  [<CompiledName "ToBinding";
    Experimental "Incompatible for Avalonia v11.1+, we're waiting for a replacement in/before v12.">]
  let toBinding<'Value> (value: cval<'Value>) =
    { new IBinding with
        member _.Initiate
          (
            target: AvaloniaObject,
            targetProperty: AvaloniaProperty,
            anchor: obj,
            enableDataValidation: bool
          ) : InstancedBinding =

          InstancedBinding.TwoWay(
            { new IObservable<obj> with
                member _.Subscribe(observer) =
                  value.AddCallback(observer.OnNext)
            },
            { new IObserver<obj> with
                member _.OnNext(newValue) =
                  match newValue with
                  | :? 'Value as newValue ->
                    transact(fun _ -> value.Value <- newValue)
                  | _ -> ()

                member _.OnError _ = ()
                member _.OnCompleted() = ()
            },
            BindingPriority.LocalValue
          )
    }


[<Class>]
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

  [<CompiledName "ToBinding";
    Extension;
    Experimental "Incompatible for Avalonia v11.1+, we're waiting for a replacement in/before v12.">]
  static member inline toBinding(value: cval<_>) = CVal.toBinding value

  [<CompiledName "ToObservable">]
  static member inline toObservable(value: aval<_>) = AVal.toObservable value

type AvaloniaRouter
  (
    routes: RouteDefinition<Control> seq,
    [<Optional>] ?splash: Func<Control>,
    [<Optional>] ?logger: ILogger
  ) =
  let router =
    let splash = splash |> Option.map(fun f -> fun () -> f.Invoke())
    Router.build<Control>(routes, ?splash = splash, ?logger = logger)


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


[<Class>]
type Route(def: RouteDefinition<Control>) =
  inherit UserControl()

  new
    (
      name: string,
      path: string,
      handler: RouteContext -> INavigable<Control> -> Control
    ) =
    Route(Route.define(name, path, handler))

  new
    (
      name: string,
      path: string,
      handler: RouteContext -> INavigable<Control> -> Async<Control>
    ) =
    Route(Route.define(name, path, handler))

  new
    (
      name: string,
      path: string,
      handler:
        RouteContext
          -> INavigable<Control>
          -> CancellationToken
          -> Task<Control>
    ) =
    Route(Route.define(name, path, handler))

  member _.Definition: RouteDefinition<Control> = def


  static member define
    (name, path, handler: RouteContext -> INavigable<Control> -> Async<Control>)
    =
    Navs.Route.define<Control>(
      name,
      path,
      fun c n -> async {
        let! result = handler c n
        return result
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
          -> Task<Control>
    ) =
    Navs.Route.define(
      name,
      path,
      fun c n t -> task {
        let! value = handler c n t
        return value
      }
    )

  static member define
    (name, path, handler: RouteContext -> INavigable<Control> -> Control)
    =
    Navs.Route.define<Control>(name, path, (fun c n -> handler c n))


[<Class>]
type Routes([<Optional>] ?logger: ILogger) as this =
  inherit UserControl()

  let children: Route[] ref = ref Array.empty

  let mutable router: IRouter<Control> | null =
    Unchecked.defaultof<IRouter<Control>>

  let buildRouter (children: Route[]) : IRouter<Control> =
    let definitions = children |> Array.map(fun c -> c.Definition)

    AvaloniaRouter(definitions, ?logger = logger) :> IRouter<Control>

  let content =
    TransitioningContentControl(
      PageTransition =
        CompositePageTransition(
          PageTransitions =
            ResizeArray [
              CrossFade(TimeSpan.FromMilliseconds 150) :> IPageTransition
              PageSlide(
                TimeSpan.FromMilliseconds 300,
                PageSlide.SlideAxis.Horizontal
              )
            ]
        )
    )

  let setupContent (router: IRouter<Control>) =
    let binding =
      TransitioningContentControl.ContentProperty
        .Bind()
        .WithMode(mode = BindingMode.OneWay)

    content[binding] <-
      router.Content
      |> AVal.map(fun content ->
        content |> ValueOption.defaultValue(UserControl())
      )
      |> AVal.toBinding

  let navigateToFirstRoute (router: IRouter<Control>, routes: Route[]) =
    match routes |> Seq.tryHead with
    | Some firstRoute ->
      async {
        let! result =
          router.NavigateByName firstRoute.Definition.name
          |> Async.AwaitTask
          |> Async.Catch

        match result with
        | Choice1Of2 _ -> return ()
        | Choice2Of2 e ->
          logger
          |> Option.iter(fun logger ->
            logger.LogError(
              e,
              "Failed to navigate to the first route: {RouteName}",
              firstRoute.Definition.name
            )
          )

          return ()
      }
      |> Async.StartImmediate
    | None -> ()

  do this[Routes.ContentProperty] <- content

  member this.Children
    with get (): Route[] = children.Value
    and set (value: Route[]) =
      let routes = defaultIfNull Array.empty value

      this.SetAndRaise(Routes.ChildrenProperty, children, routes) |> ignore

      router <- buildRouter routes
      let router = nonNull router
      setupContent router
      navigateToFirstRoute(router, routes)

  member this.Router =
    match router with
    | null -> ValueNone
    | router -> ValueSome router


  static member ChildrenProperty =
    AvaloniaProperty.RegisterDirect<Routes, Route[]>(
      "Children",
      _.Children,
      (fun o v -> o.Children <- v)
    )


type RoutesExtensions =

  [<Extension>]
  static member inline Children
    (control: Routes, [<ParamArray>] routes: Route[])
    =
    control.Children <- routes
    control


module Interop =

  type Route =
    [<CompiledName "Define">]
    static member inline define
      (name, path, handler: Func<RouteContext, INavigable<Control>, Control>)
      =
      Navs.Route.define(name, path, (fun c n -> handler.Invoke(c, n)))

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
            Task<Control>
           >
      ) =
      Navs.Route.define(
        name,
        path,
        (fun c n t -> task {
          let! value = handler.Invoke(c, n, t)
          return value
        })
      )

type RouterOutlet() as this =
  inherit UserControl()

  let router: (IRouter<_> | null) ref = ref Unchecked.defaultof<IRouter<_>>

  let pageTransition: (IPageTransition | null) ref =
    ref Unchecked.defaultof<IPageTransition>

  let noContent: (Control | null) ref = ref Unchecked.defaultof<Control>

  let setupContent () =

    match router.Value with
    | null -> this.Content <- null
    | router ->
      let noContent =
        match noContent.Value with
        | null -> TextBlock(Text = "No Content") :> Control
        | value -> value

      let content =
        router.Content
        |> AVal.map(ValueOption.defaultValue noContent)
        |> AVal.toBinding

      let pageTransition =
        match pageTransition.Value with
        | null ->
          CompositePageTransition(
            PageTransitions =
              ResizeArray [
                CrossFade(TimeSpan.FromMilliseconds 150) :> IPageTransition
                PageSlide(
                  TimeSpan.FromMilliseconds 300,
                  PageSlide.SlideAxis.Horizontal
                )
              ]
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

        transitionContent[binding] <- content
        transitionContent

      this[RouterOutlet.ContentProperty] <- transitionContent

  member this.Router
    with get (): IRouter<Control> | null = router.Value
    and set (value: IRouter<Control> | null) =
      this.SetAndRaise(RouterOutlet.RouterProperty, router, value) |> ignore
      setupContent()

  member this.PageTransition
    with get () = pageTransition.Value
    and set (value: IPageTransition | null) =
      this.SetAndRaise(
        RouterOutlet.PageTransitionProperty,
        pageTransition,
        value
      )
      |> ignore

  member this.NoContent
    with get () = noContent.Value
    and set (value: Control | null) =
      this.SetAndRaise(RouterOutlet.NoContentProperty, noContent, value)
      |> ignore

  static member RouterProperty =
    AvaloniaProperty.RegisterDirect<RouterOutlet, IRouter<Control> | null>(
      "Router",
      _.Router,
      (fun o v -> o.Router <- v)
    )

  static member PageTransitionProperty =
    AvaloniaProperty.RegisterDirect<RouterOutlet, IPageTransition | null>(
      "PageTransition",
      _.PageTransition,
      (fun o v -> o.PageTransition <- v)
    )

  static member NoContentProperty =
    AvaloniaProperty.RegisterDirect<RouterOutlet, Control | null>(
      "NoContent",
      _.NoContent,
      (fun o v -> o.NoContent <- v)
    )

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

    let descriptor =
      RouterOutlet.PageTransitionProperty.Bind().WithMode(mode = mode)

    routerOutlet[descriptor] <- (AVal.toObservable pageTransition).ToBinding()
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

    let descriptor = RouterOutlet.NoContentProperty.Bind().WithMode(mode = mode)

    routerOutlet[descriptor] <- (AVal.toObservable noContent).ToBinding()
    routerOutlet
