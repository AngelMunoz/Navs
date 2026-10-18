module FuncUITests

open System.Threading
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Expecto
open Navs
open Navs.Router

let makeView () : IView = TextBlock.create []

[<Tests>]
let tests =
  testList "FuncUI adapter" [
    testCase "FuncUIRouter renders a view and reaches Idle"
    <| fun () ->
      let routes = [
        Route.define<IView>("home", "/", fun _ _ -> makeView())
        Route.define<IView>("about", "/about", fun _ _ -> makeView())
      ]

      let router = Navs.FuncUI.FuncUIRouter(routes) :> IRouter<IView>

      let result =
        router.Navigate ("/") CancellationToken.None
        |> fun task -> task.GetAwaiter().GetResult()

      Expect.isOk result "navigation should succeed"

      Expect.isTrue
        router.Snapshot.Content.IsValueSome
        "content should be present"

      Expect.equal router.Snapshot.State Idle "state should be Idle"
  ]
