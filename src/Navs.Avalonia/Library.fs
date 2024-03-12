namespace Navs.Avalonia

open System
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks

open Avalonia.Controls

open FSharp.Data.Adaptive

open Navs
open Navs.Router

module AVal =
  open Avalonia.Data

  let useState<'Value>
    (initialValue: 'Value)
    : aval<'Value> * ('Value -> unit) =
    let v = cval initialValue
    let update value = transact(fun () -> v.Value <- value)

    v, update

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

    let UseState<'Value>
      (initialValue: 'Value)
      : aval<'Value> * Action<'Value> =
      let value = cval initialValue
      value, (Action<'Value>(fun v -> transact(fun () -> value.Value <- v)))

type AvaloniaRouter(routes, [<Optional>] ?splash: Func<Control>) =
  let router =
    let splash = splash |> Option.map(fun f -> fun () -> f.Invoke())
    Router.get<Control>(routes, ?splash = splash)


  interface IRouter<Control> with
    member _.Content = router.Content

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
