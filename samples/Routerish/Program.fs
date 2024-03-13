open System

open Avalonia
open Avalonia.Controls
open Avalonia.Data

open NXUI.Desktop
open NXUI.FSharp.Extensions

open Navs
open Navs.Avalonia

let routes = [
  Route.define(
    "guid",
    "/:id<guid>",
    fun context _ -> async {
      return
        match context.urlMatch.Params.TryGetValue "id" with
        | true, id -> TextBlock().text($"%O{id}")
        | false, _ -> TextBlock().text("Guid No GUID")
    }
  )
  Route.define("books", "/books", (fun _ _ -> TextBlock().text("Books")))
]

let getMainContent (router: IRouter<Control>) =
  ContentControl()
    .DockTop()
    .content(router.Content |> AVal.toBinding, BindingMode.OneWay)

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
    AvaloniaRouter(routes, splash = (fun _ -> TextBlock().text("Loading...")))

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
                .OnClickHandler(navigate $"/{Guid.NewGuid()}" router)
            ),
          getMainContent(router)
        )
    )


NXUI.Run(app, "Navs.Avalonia!", Environment.GetCommandLineArgs()) |> ignore
