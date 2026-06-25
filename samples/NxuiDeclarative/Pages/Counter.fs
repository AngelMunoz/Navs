module Pages.Counter

open System
open System.Threading
open System.Threading.Tasks
open IcedTasks
open NXUI.Extensions
open Avalonia.Controls
open Avalonia.Controls.Templates
open FSharp.Data.Adaptive
open IcedTasks
open Navs
open Navs.Avalonia

let view: SyncView<Control> =
  fun ctx _ ->
    let initialState = defaultValueArg (ctx.getParam<int> "count") 0
    let value, setValue = AVal.useState initialState

    let increment () = setValue(fun v -> v + 1)
    let decrement () = setValue(fun v -> v - 1)
    let reset () = setValue(fun _ -> 0)

    let text = value |> AVal.map(fun v -> $"Count: {v}")

    StackPanel()
      .Spacing(8)
      .Children(
        Button().Content("Increment").OnClickHandler(fun _ _ -> increment()),
        Button().Content("Decrement").OnClickHandler(fun _ _ -> decrement()),
        Button().Content("Reset").OnClickHandler(fun _ _ -> reset()),
        TextBlock().Text(text |> AVal.toBinding)
      )
    :> Control
