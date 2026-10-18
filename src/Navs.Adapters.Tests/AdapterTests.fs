module AdapterTests

open System
open System.Threading
open Expecto
open Navs
open Navs.Router

let navigate (router: IRouter<'View>) url =
  router.Navigate (url) CancellationToken.None
  |> fun task -> task.GetAwaiter().GetResult()

let block text =
  let value = new Avalonia.Controls.TextBlock()
  value.Text <- text
  value :> Avalonia.Controls.Control

let transitionOf (outlet: Navs.Avalonia.RouterOutlet) =
  match outlet.Content with
  | :? Avalonia.Controls.TransitioningContentControl as value -> value
  | _ -> failtest "the outlet should expose a TransitioningContentControl"

let renderedText (control: obj | null) =
  match control with
  | :? Avalonia.Controls.TextBlock as value -> value.Text
  | _ -> failtest "the outlet should render a TextBlock"

let onlyWindow (outlet: Navs.Terminal.Gui.RouterOutlet) =
  match outlet.SubViews |> Seq.tryHead with
  | Some(:? Terminal.Gui.Views.Window as value) -> value
  | _ -> failtest "the outlet should contain a Window"

[<Tests>]
let tests =
  testList "Navs adapters" [
    testCase "Avalonia router and outlet render the active view"
    <| fun () ->
      let routes = [
        Route.define<Avalonia.Controls.Control>(
          "home",
          "/",
          fun _ _ -> block "Home"
        )
        Route.define<Avalonia.Controls.Control>(
          "about",
          "/about",
          fun _ _ -> block "About"
        )
      ]

      let router =
        Navs.Avalonia.AvaloniaRouter(routes)
        :> IRouter<Avalonia.Controls.Control>

      Expect.isOk (navigate router "/") "initial navigation should succeed"

      let outlet = Navs.Avalonia.RouterOutlet()
      outlet.Router <- Some router

      let transition = transitionOf outlet

      Expect.equal
        (renderedText transition.Content)
        "Home"
        "the outlet should render Home"

      Expect.isOk (navigate router "/about") "second navigation should succeed"

      Expect.equal
        (renderedText transition.Content)
        "About"
        "the outlet should render About"

    testCase "Avalonia outlet renders splash and no-content fallback"
    <| fun () ->
      let routes = [
        Route.define<Avalonia.Controls.Control>(
          "home",
          "/",
          fun _ _ -> block "Home"
        )
      ]

      let splashRouter =
        Navs.Avalonia.AvaloniaRouter(
          routes,
          splash = Func<Avalonia.Controls.Control>(fun () -> block "Splash")
        )
        :> IRouter<Avalonia.Controls.Control>

      let outlet = Navs.Avalonia.RouterOutlet()
      outlet.Router <- Some splashRouter

      let transition = transitionOf outlet

      Expect.equal
        (renderedText transition.Content)
        "Splash"
        "the outlet should render the splash"

      Expect.isOk (navigate splashRouter "/") "navigation should succeed"

      Expect.equal
        (renderedText transition.Content)
        "Home"
        "the outlet should render Home"

      let emptyRouter =
        Navs.Avalonia.AvaloniaRouter(routes)
        :> IRouter<Avalonia.Controls.Control>

      let emptyOutlet = Navs.Avalonia.RouterOutlet()
      emptyOutlet.NoContent <- Some(block "No content")
      emptyOutlet.Router <- Some emptyRouter

      Expect.equal
        (renderedText (transitionOf emptyOutlet).Content)
        "No content"
        "the outlet should render NoContent"

    testCase "Avalonia outlet unsubscribes from the previous router"
    <| fun () ->
      let routesA = [
        Route.define<Avalonia.Controls.Control>(
          "home",
          "/",
          fun _ _ -> block "Router A"
        )
      ]

      let routesB = [
        Route.define<Avalonia.Controls.Control>(
          "home",
          "/",
          fun _ _ -> block "Router B"
        )
      ]

      let routerA =
        Navs.Avalonia.AvaloniaRouter(routesA)
        :> IRouter<Avalonia.Controls.Control>

      let routerB =
        Navs.Avalonia.AvaloniaRouter(routesB)
        :> IRouter<Avalonia.Controls.Control>

      Expect.isOk (navigate routerA "/") "router A should navigate"
      Expect.isOk (navigate routerB "/") "router B should navigate"

      let outlet = Navs.Avalonia.RouterOutlet()
      outlet.Router <- Some routerA

      let transition = transitionOf outlet

      Expect.equal
        (renderedText transition.Content)
        "Router A"
        "the outlet should render router A"

      outlet.Router <- Some routerB

      Expect.equal
        (renderedText transition.Content)
        "Router B"
        "the outlet should render router B"

      navigate routerA "/" |> ignore

      Expect.equal
        (renderedText transition.Content)
        "Router B"
        "the outlet should no longer react to router A changes"

    testCase "Terminal.Gui router and outlet replace the active window"
    <| fun () ->
      let makeWindow title =
        let window = new Terminal.Gui.Views.Window()
        window.Title <- title
        window

      let routes = [
        Route.define<Terminal.Gui.Views.Window>(
          "home",
          "/",
          fun _ _ -> makeWindow "Home"
        )
        Route.define<Terminal.Gui.Views.Window>(
          "about",
          "/about",
          fun _ _ -> makeWindow "About"
        )
      ]

      let router =
        Navs.Terminal.Gui.TerminalGuiRouter(routes)
        :> IRouter<Terminal.Gui.Views.Window>

      Expect.isOk (navigate router "/") "initial navigation should succeed"

      let outlet = new Navs.Terminal.Gui.RouterOutlet(router)

      Expect.equal
        outlet.SubViews.Count
        1
        "the outlet should contain one window"

      Expect.equal
        (onlyWindow outlet).Title
        "Home"
        "the outlet should render Home"

      Expect.isOk (navigate router "/about") "second navigation should succeed"

      Expect.equal
        outlet.SubViews.Count
        1
        "the outlet should still contain one window"

      Expect.equal
        (onlyWindow outlet).Title
        "About"
        "the outlet should render About"
  ]
