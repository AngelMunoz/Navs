open System

open Avalonia
open Avalonia.Controls
open Avalonia.Data
open NXUI.Desktop
open NXUI.FSharp.Extensions

open FSharp.Data.Adaptive
open Navs
open Navs.Avalonia
open Microsoft.Extensions.Logging

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
        | true, id -> TextBlock().text($"%O{id}") :> Control
        | false, _ -> TextBlock().text("Guid No GUID")
    }
  )
  Route.define(
    "books",
    "/books",
    (fun _ _ -> TextBlock().text("Books") :> Control)
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
        .spacing(8)
        .children(
          Button().content("Increment").OnClickHandler(fun _ _ -> increment()),
          Button().content("Decrement").OnClickHandler(fun _ _ -> decrement()),
          Button().content("Reset").OnClickHandler(fun _ _ -> reset()),
          TextBlock().text(text |> AVal.toBinding)
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
      splash = (fun _ -> TextBlock().text("Loading...")),
      logger = logger
    )

  Window()
    .content(
      DockPanel()
        .lastChildFill(true)
        .children(
          StackPanel()
            .DockTop()
            .OrientationHorizontal()
            .spacing(8)
            .children(
              Button().content("Books").OnClickHandler(navigate "/books" router),
              Button()
                .content("Guid")
                .OnClickHandler(navigate $"/{Guid.NewGuid()}" router),
              Button()
                .content("Counter")
                .OnClickHandler(navigate "/counter" router),
              Button()
                .content("Counter with query")
                .OnClickHandler(navigate "/counter?count=10" router)
            ),
          RouterOutlet().DockTop().router(router)
        )
    )


NXUI.Run(app, "Navs.Avalonia!", Environment.GetCommandLineArgs()) |> ignore
