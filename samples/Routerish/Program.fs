open System

open IcedTasks

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
    ":id<guid>",
    fun context -> task {
      return
        match context.UrlMatch.Params.TryGetValue "id" with
        | true, id -> TextBlock().text(sprintf "Home %A" id) :> Control
        | false, _ -> TextBlock().text("Guid No GUID")
    }
  )
  Route.define<Control>(
    "guid",
    ":id<guid>",
    fun context -> task {
      return
        match context.UrlMatch.Params.TryGetValue "id" with
        | true, id -> TextBlock().text(sprintf "Home %A" id) :> Control
        | false, _ -> TextBlock().text("Guid No GUID")
    }
  )
  Route.define(
    "users",
    "users",
    fun _ -> TextBlock().text("Users") :> Control
  )
  |> Route.child(
    Route.define<Control>(
      "user-detail",
      ":id<int>",
      fun context -> async {
        return
          match context.UrlMatch.Params.TryGetValue "id" with
          | true, id -> TextBlock().text(sprintf "User %A" id) :> Control
          | false, _ -> TextBlock().text("User but No Id")
      }
    )
    |> Route.child(
      Route.define<Control>(
        "user-profile",
        "profile",
        fun context -> task {
          return
            match context.UrlMatch.Params.TryGetValue "id" with
            | true, id -> TextBlock().text(sprintf "Profile %A" id) :> Control
            | false, _ -> TextBlock().text("Profile but No Id")
        }
      )
    )
  )
  Route.define<Control>(
    "books",
    "books",
    (fun _ -> TextBlock().text("Books") :> Control)
  )
]

let navigateBack (router: Router<_>) _ _ =
  vTask {
    match! router.Back() with
    | Ok _ -> return ()
    | Error e -> printfn "%A" e
  }
  |> ignore

let navigateForward (router: Router<_>) _ _ =
  vTask {
    match! router.Forward() with
    | Ok _ -> return ()
    | Error e -> printfn "%A" e
  }
  |> ignore

let navigateHome (router: Router<_>) _ _ =
  vTask {
    match! router.Navigate("/") with
    | Ok _ -> return ()
    | Error e -> printfn "%A" e
  }
  |> ignore

let navigateAbout (router: Router<_>) _ _ =
  vTask {
    match! router.Navigate("/books") with
    | Ok _ -> return ()
    | Error e -> printfn "%A" e
  }
  |> ignore

let navigateGuid (router: Router<_>) _ _ =
  vTask {
    match! router.Navigate($"/{Guid.NewGuid()}") with
    | Ok _ -> return ()
    | Error e -> printfn "%A" e
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
            Button().content("Home").OnClickHandler(navigateHome router),
            Button().content("About").OnClickHandler(navigateAbout router),
            Button().content("Guid").OnClickHandler(navigateGuid router)
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
