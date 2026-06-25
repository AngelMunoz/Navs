open System

open Avalonia
open Avalonia.Controls
open Avalonia.Data
open NXUI.Desktop
open NXUI.Extensions

open FSharp.Data.Adaptive
open Navs
open Navs.Avalonia
open Microsoft.Extensions.Logging
open IcedTasks
open IcedTasks.Polyfill.Async.PolyfillBuilders

let lf =
  LoggerFactory.Create(fun builder ->
    builder.AddConsole().SetMinimumLevel LogLevel.Trace |> ignore
  )

let logger = lf.CreateLogger "Navs.Avalonia.Program"

let routes = [
  Route.define(
    "guid",
    "/:id<guid>",
    fun context _ -> async {

      return
        match context.urlMatch.Params.TryGetValue "id" with
        | true, id -> TextBlock().Text($"%O{id}") :> Control
        | false, _ -> TextBlock().Text("Guid No GUID")
    }
  )
  Route.define(
    "books",
    "/books",
    (fun _ _ -> TextBlock().Text("Books") :> Control)
  )
  Route.define(
    "counter",
    "/counter?count<int>",
    (fun ctx _ ->
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
    )
  )
  |> Route.cache NoCache
]


let navigate url (router: IRouter<Control>) _ _ =
  async {
    let! result = router.Navigate(url) |> Async.AwaitTask

    match result with
    | Ok _ -> ()
    | Error e -> printfn $"%A{e}"
  }
  |> Async.StartImmediate

let app () =

  let router =
    AvaloniaRouter(
      routes,
      splash = (fun _ -> TextBlock().Text("Loading...")),
      logger = logger
    )

  Window()
    .Content(
      DockPanel()
        .LastChildFill(true)
        .Children(
          StackPanel()
            .DockTop()
            .OrientationHorizontal()
            .Spacing(8)
            .Children(
              Button().Content("Books").OnClickHandler(navigate "/books" router),
              Button()
                .Content("Guid")
                .OnClickHandler(navigate $"/{Guid.NewGuid()}" router),
              Button()
                .Content("Counter")
                .OnClickHandler(navigate "/counter" router),
              Button()
                .Content("Counter with query")
                .OnClickHandler(navigate "/counter?count=10" router)
            ),
          RouterOutlet().router(router).DockTop()
        )
    )


NXUI.Run(app, "Navs.Avalonia!", Environment.GetCommandLineArgs()) |> ignore
