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
    (
      name,
      path,
      handler: RouteContext -> INavigable<Control> -> Async<#Control>
    ) : RouteDefinition<Control> =
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
    (
      name,
      path,
      handler: RouteContext -> INavigable<Control> -> #Control
    ) : RouteDefinition<Control> =
    Navs.Route.define<Control>(name, path, (fun c n -> handler c n :> Control))

module Interop =

  type Route =
    [<CompiledName "Define">]
    static member inline define
      (
        name,
        path,
        handler: Func<RouteContext, INavigable<Control>, #Control>
      ) =
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
          Func<RouteContext, INavigable<Control>, CancellationToken, Task<#Control>>
      ) =
      Navs.Route.define(
        name,
        path,
        (fun c n t -> task {
          let! value = handler.Invoke(c, n, t)
          return value :> Control
        })
      )
