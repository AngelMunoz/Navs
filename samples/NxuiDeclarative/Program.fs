open System
open System.Threading

open Avalonia.Controls
open NXUI.Desktop
open NXUI.Extensions

open Navs
open Navs.Router
open Navs.Avalonia

let block text = TextBlock().Text(text) :> Control

let routes = [
  Route.define<Control>(
    "guid",
    "/?id<guid>",
    fun context _ ->
      match context.GetParam<Guid>("id") with
      | ValueSome id -> block $"Guid: {id}"
      | ValueNone -> block "No GUID provided"
  )

  Route.define<Control>(
    "books",
    "/books?title",
    fun context _ ->
      let title =
        context.GetParam<string>("title")
        |> ValueOption.defaultValue "all books"

      block $"Books: {title}"
  )

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
]

let navigate (url: string) (router: IRouter<Control>) =
  task {
    match! router.Navigate url CancellationToken.None with
    | Ok _ -> ()
    | Error error -> eprintfn "Navigation failed for %s: %A" url error
  }
  |> ignore

let app () =
  let router = AvaloniaRouter(routes) :> IRouter<Control>

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
                .OnClickHandler(fun _ _ ->
                  navigate $"/?id={Guid.NewGuid()}" router
                ),
              Button()
                .Content("Counter")
                .OnClickHandler(fun _ _ -> navigate "/counter" router),
              Button()
                .Content("Counter with query")
                .OnClickHandler(fun _ _ -> navigate "/counter?count=10" router),
              Button()
                .Content("Books with query")
                .OnClickHandler(fun _ _ -> navigate "/books?title=The" router)
            ),
          outlet.DockTop()
        )
    )

NXUI.Run(app, "Navs.Avalonia!", Environment.GetCommandLineArgs()) |> ignore
