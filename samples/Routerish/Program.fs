open System

open Avalonia
open Avalonia.Controls
open Avalonia.Data

open NXUI.Desktop
open NXUI.FSharp.Extensions

open Navs
open Navs.Router

let routes: RouteDefinition<Control> list = [
  Route.define<Control>(
    "guid",
    "/:id<guid>",
    fun context -> async {
      return
        match context.UrlMatch.Params.TryGetValue "id" with
        | true, id -> TextBlock().text(sprintf "Home %A" id) :> Control
        | false, _ -> TextBlock().text("Guid No GUID")
    }
  )
  Route.define<Control>(
    "books",
    "/books",
    (fun _ -> TextBlock().text("Books") :> Control)
  )
]

let navigate url (router: Router<Control>) _ _=
  task {
    let! result = router.Navigate(url)
    match result  with
    | Ok _ -> ()
    | Error e -> printfn $"%A{e}"
  }
  |> ignore


let startApp () =
  let router = Router(RouteTracks.fromDefinitions routes)

  let content =
    router.Content
    |> Observable.map(fun value ->
      match value with
      | ValueSome value -> value
      | _ -> TextBlock().text("No Content")
    )

  let shell =
    DockPanel()
      .lastChildFill(true)
      .children(
        StackPanel()
          .DockTop()
          .spacing(8)
          .children(
            Button().content("Books").OnClickHandler(navigate "/books" router),
            Button().content("Guid").OnClickHandler(navigate $"/{Guid.NewGuid()}" router)
          ),
        ContentControl()
          .DockTop()
          .content(content.ToBinding(), BindingMode.OneWay)
      )

  Window().content(shell).minWidth(800.).minHeight(600.)

[<EntryPoint; STAThread>]
let main argv =

  NXUI.Run(
    startApp,
    "Routerish!",
    argv,
    themeVariant = Styling.ThemeVariant.Dark
  )
