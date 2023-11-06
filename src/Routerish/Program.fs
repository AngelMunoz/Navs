open System

open IcedTasks

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Data

open NXUI.Desktop
open NXUI.FSharp.Extensions

open FsToolkit.ErrorHandling

open Navs


// let inline navigateBack (router: Router<_>) _ _ =
//   vTask {
//     match! router.Back() with
//     | Ok _ -> return ()
//     | Error e -> printfn "%A" e
//   }
//   |> ignore

// let inline navigateForward (router: Router<_>) _ _ =
//   vTask {
//     match! router.Forward() with
//     | Ok _ -> return ()
//     | Error e -> printfn "%A" e
//   }
//   |> ignore

// let inline navigateHome (router: Router<_>) _ _ =
//   vTask {
//     match! router.Navigate("") with
//     | Ok _ -> return ()
//     | Error e -> printfn "%A" e
//   }
//   |> ignore

// let inline navigateAbout (router: Router<_>) _ _ =
//   vTask {
//     match! router.Navigate("about") with
//     | Ok _ -> return ()
//     | Error e -> printfn "%A" e
//   }
//   |> ignore

// let inline navigateGuid (router: Router<_>) _ _ =
//   vTask {
//     match! router.Navigate($"{Guid.NewGuid()}") with
//     | Ok _ -> return ()
//     | Error e -> printfn "%A" e
//   }
//   |> ignore

// let routes =
//   [
//     Route.route<Control> ""
//     |> Route.path ""
//     |> Route.view(fun _ -> cancellableValueTask {
//       return TextBlock().text("Home")
//     })
//     |> Route.define

//     Route.route<Control> ""
//     |> Route.path "about"
//     |> Route.view(fun _ -> cancellableValueTask {
//       return TextBlock().text("About")
//     })
//     |> Route.define

//     Route.route<Control> ""
//     |> Route.path ":id<guid>"
//     |> Route.view(fun context -> cancellableValueTask {
//       match context.UrlMatch.Params |> Map.tryFind "id" with
//       | Some id -> return TextBlock().text(sprintf "Home %A" id)
//       | None -> return TextBlock().text("Guid No GUID")
//     })
//     |> Route.define
//   ]
//   |> List.traverseResultA id
//   |> Result.mapError(sprintf "%A")
//   |> Result.bind Mapper.mapDefinitions

// let startApp definedRoutes (app: IClassicDesktopStyleApplicationLifetime) =
//   let router = Router(definedRoutes)

//   let shell =
//     DockPanel()
//       .lastChildFill(true)
//       .children(
//         StackPanel()
//           .DockTop()
//           .spacing(8)
//           .children(
//             Button().content("Home").OnClickHandler(navigateHome router),
//             Button().content("About").OnClickHandler(navigateAbout router),
//             Button().content("Guid").OnClickHandler(navigateGuid router)
//           ),
//         ContentControl()
//           .DockTop()
//           .content(router.ViewContent.ToBinding(), BindingMode.OneWay)
//       )

//   app.MainWindow <- Window().content(shell).minWidth(800.).minHeight(600.)


// [<EntryPoint>]
// let main argv =
//   match routes with
//   | Ok definedRoutes ->

//     NXUI.Run((startApp definedRoutes), "Routerish Demo!", [||])
//   | Error e ->
//     for e in e do
//       printfn "%A" e

//     1

open Navs.Experiments
open Navs.RouterB
open FSharp.Data.Adaptive


let routes = [
  Route.defineResolve(
    "home",
    ":id<guid>",
    fun context -> cancellableValueTask {
      return
        match context.UrlMatch.Params.TryGetValue "id" with
        | true, id -> TextBlock().text(sprintf "Home %A" id)
        | false, _ -> TextBlock().text("Guid No GUID")
    }
  )
  Route.define("users", "users", TextBlock().text("Users"))
  |> Route.child(
    Route.defineResolve(
      "user-detail",
      ":id<int>",
      fun context -> cancellableValueTask {
        return
          match context.UrlMatch.Params.TryGetValue "id" with
          | true, id -> TextBlock().text(sprintf "User %A" id)
          | false, _ -> TextBlock().text("User but No Id")
      }
    )
    |> Route.child(
      Route.define("user-profile", "profile", TextBlock().text("Profile"))
    )
  )
  Route.define("books", "books", TextBlock().text("Books"))
]

let ha = routes |> RouteTrack.ofDefinitions


let r = Router<_>(ha)

r.Content.AddCallback(fun (a: TextBlock voption) ->
  match a with
  | ValueSome a -> printfn "%A" a.Text
  | ValueNone -> printfn "None"
)
|> ignore

r.Navigate("/users").GetAwaiter().GetResult() |> printfn "%A"
r.Navigate("/users/123").GetAwaiter().GetResult() |> printfn "%A"
r.Navigate("/users/123/profile").GetAwaiter().GetResult() |> printfn "%A"
r.Navigate("/users/123").GetAwaiter().GetResult() |> printfn "%A"
r.Navigate("/users").GetAwaiter().GetResult() |> printfn "%A"
