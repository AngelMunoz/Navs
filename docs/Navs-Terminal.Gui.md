# Navs.Terminal.Gui

`Navs.Terminal.Gui` binds the core router to Terminal.Gui `Window` values. The adapter targets `net10.0`.

## Define routes

```fsharp
open Terminal.Gui.Views
open Navs
open Navs.Router
open Navs.Terminal.Gui

let makeWindow title =
    let window = new Window()
    window.Title <- title
    window

let routes = [
    Route.define<Window>("home", "/", fun _ _ -> makeWindow "Home")
    Route.define<Window>("about", "/about", fun _ _ -> makeWindow "About")
]
```

## Create the router and outlet

```fsharp
let router = TerminalGuiRouter(routes) :> IRouter<Window>
let outlet = new RouterOutlet(router)
```

`RouterOutlet` removes the previous window and adds the current one whenever `IRouter.Changed` fires. It starts the initial navigation on construction.

## Breaking change

The adapter no longer exposes an adapter-specific `Route` wrapper or a `Route` interop class. Use the core `Route.define` and pass `RouteDefinition<Window>` values to `TerminalGuiRouter`.
