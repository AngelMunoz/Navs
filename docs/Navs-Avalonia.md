# Navs.Avalonia

`Navs.Avalonia` binds the core router to Avalonia `Control` values.

## Define routes

```fsharp
open System.Threading
open Avalonia.Controls
open Navs
open Navs.Router
open Navs.Avalonia

let block text =
    let value = TextBlock()
    value.Text <- text
    value :> Control

let routes = [
    Route.define<Control>("home", "/", fun _ _ -> block "Home")
    Route.define<Control>("about", "/about", fun _ _ -> block "About")
]
```

## Create the router

```fsharp
let router = AvaloniaRouter(routes) :> IRouter<Control>
```

The constructor accepts an optional splash factory:

```fsharp
let router =
    AvaloniaRouter(
        routes,
        splash = System.Func<Control>(fun () -> block "Loading...")
    ) :> IRouter<Control>
```

## Render the outlet

```fsharp
let outlet = RouterOutlet()
outlet.Router <- Some router
outlet.PageTransition <- Avalonia.Animation.CrossFade(System.TimeSpan.FromMilliseconds 150.)
outlet.NoContent <- Some(block "No content")
```

The outlet subscribes to `IRouter.Changed` and renders `Snapshot.Content`. It does not own routes or navigation rules.

## Navigate

```fsharp
router.Navigate("/about") CancellationToken.None |> ignore
```

Navigation returns `CancellableTask<Result<...>>`; invoke it with the token. For UI event handlers, use the token from the event or `CancellationToken.None`.

## Breaking change

The adapter no longer exposes a `Routes` control or an adapter-specific `Route` wrapper. Register routes with the core `Route.define` and pass the resulting `RouteDefinition<Control>` values to `AvaloniaRouter`.
