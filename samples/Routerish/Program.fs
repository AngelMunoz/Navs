open System
open System.Threading

open Avalonia.Controls
open NXUI.Desktop
open NXUI.Extensions

open Navs
open Navs.Router
open Navs.Avalonia
open Microsoft.Extensions.Logging

let lf =
  LoggerFactory.Create(fun builder ->
    builder.AddConsole().SetMinimumLevel LogLevel.Trace |> ignore
  )

let logger = lf.CreateLogger "Navs.Avalonia.Program"

let block text = TextBlock().Text(text) :> Control

let routes = [
  Route.define<Control>(
    "guid",
    "/:id<guid>",
    fun context _ -> async {
      return
        match context.GetParam<Guid>("id") with
        | ValueSome id -> block $"%O{id}"
        | ValueNone -> block "Guid No GUID"
    }
  )

  Route.define<Control>("books", "/books", fun _ _ -> block "Books")

  Route.define<Control>(
    "counter",
    "/counter?count<int>",
    fun context _ ->
      let mutable count =
        context.GetParam<int>("count") |> ValueOption.defaultValue 0

      let text = TextBlock().Text($"Count: {count}")

      let update () = text.Text <- $"Count: {count}"

      StackPanel()
        .Spacing(8)
        .Children(
          Button()
            .Content("Increment")
            .OnClickHandler(fun _ _ ->
              count <- count + 1
              update()
            ),
          Button()
            .Content("Decrement")
            .OnClickHandler(fun _ _ ->
              count <- count - 1
              update()
            ),
          Button()
            .Content("Reset")
            .OnClickHandler(fun _ _ ->
              count <- 0
              update()
            ),
          text
        )
      :> Control
  )
  |> Route.cache NoCache
]

let navigate (url: string) (router: IRouter<Control>) =
  router.Navigate (url) CancellationToken.None
  |> fun task -> task.GetAwaiter().GetResult()
  |> ignore

let app () =
  let router =
    AvaloniaRouter(
      routes,
      splash = Func<Control>(fun () -> block "Loading..."),
      logger = logger
    )
    :> IRouter<Control>

  let outlet = RouterOutlet()
  outlet.Router <- Some router

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
              Button()
                .Content("Books")
                .OnClickHandler(fun _ _ -> navigate "/books" router),
              Button()
                .Content("Guid")
                .OnClickHandler(fun _ _ -> navigate $"/{Guid.NewGuid()}" router),
              Button()
                .Content("Counter")
                .OnClickHandler(fun _ _ -> navigate "/counter" router),
              Button()
                .Content("Counter with query")
                .OnClickHandler(fun _ _ -> navigate "/counter?count=10" router)
            ),
          outlet.DockTop()
        )
    )

NXUI.Run(app, "Navs.Avalonia!", Environment.GetCommandLineArgs()) |> ignore
