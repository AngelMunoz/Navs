open System

open Avalonia
open Avalonia.Controls
open Avalonia.Data
open NXUI.Desktop
open NXUI.Extensions

open IcedTasks
open IcedTasks.Polyfill.Async.PolyfillBuilders

open FSharp.Data.Adaptive
open Navs
open Navs.Avalonia
open Microsoft.Extensions.Logging
open Pages

let lf =
  LoggerFactory.Create(fun builder ->
    builder.AddConsole().SetMinimumLevel LogLevel.Trace |> ignore
  )

let logger = lf.CreateLogger "Navs.Avalonia.Program"

let navigate url (router: IRouter<Control> voption) _ _ =
  async {
    match router with
    | ValueNone ->
      logger.LogInformation "Router is not initialized."
      return ()
    | ValueSome router ->
      let! result = router.Navigate url |> Async.AwaitTask

      match result with
      | Ok _ -> ()
      | Error e -> logger.LogError("Navigation error: {Error}", e)

      return ()
  }
  |> Async.StartImmediate


let AppRoutes (logger) =
  Routes(logger = logger)
    .Children(
      Route("guid", "/?id<guid>", Guid.view),
      Route("books", "/books?title", Books.view),
      Route("counter", "/counter?count<int>", Counter.view)
    )

let app () =
  let routes = AppRoutes logger
  let router = routes.Router

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
                .OnClickHandler(navigate $"/?id={Guid.NewGuid()}" router),
              Button()
                .Content("Counter")
                .OnClickHandler(navigate "/counter" router),
              Button()
                .Content("Counter with query")
                .OnClickHandler(navigate "/counter?count=10" router),
              Button()
                .Content("Books with query")
                .OnClickHandler(navigate "/books?title=The" router)
            ),
          routes
        )
    )


NXUI.Run(app, "Navs.Avalonia!", Environment.GetCommandLineArgs()) |> ignore
