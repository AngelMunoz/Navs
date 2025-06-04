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
                .OnClickHandler(navigate $"/?id={Guid.NewGuid()}" router),
              Button()
                .content("Counter")
                .OnClickHandler(navigate "/counter" router),
              Button()
                .content("Counter with query")
                .OnClickHandler(navigate "/counter?count=10" router),
              Button()
                .content("Books with query")
                .OnClickHandler(navigate "/books?title=The" router)
            ),
          routes
        )
    )


NXUI.Run(app, "Navs.Avalonia!", Environment.GetCommandLineArgs()) |> ignore
