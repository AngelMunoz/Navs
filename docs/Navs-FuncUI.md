# Navs.FuncUI

`Navs.FuncUI` binds the core router to Avalonia.FuncUI `IView` values.

## Define routes

```fsharp
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Navs
open Navs.Router
open Navs.FuncUI

let view text =
    TextBlock.create [ TextBlock.text text ]

let routes = [
    Route.define<IView>("home", "/", fun _ _ -> view "Home")
    Route.define<IView>("about", "/about", fun _ _ -> view "About")
]
```

## Create the router

```fsharp
let router = FuncUIRouter(routes) :> IRouter<IView>
```

## Render the outlet

```fsharp
let outlet =
    RouterOutlet.create(
        router,
        initialUri = "/",
        noContent = view "No content"
    )
```

`RouterOutlet.create` subscribes to `IRouter.Changed` and renders the current content. It starts the initial navigation unless `initialUri` is omitted.

## Use the router in a component

```fsharp
let content (router: IRouter<IView>) =
    Component.create("content", fun context ->
        let current = context.useRouter router
        current.Current
    )
```

`useRouter` returns an `IReadable<IView voption>` and updates it when the router snapshot changes.

## Breaking change

The adapter no longer exposes an adapter-specific `Route` wrapper. Use the core `Route.define` and pass `RouteDefinition<IView>` values to `FuncUIRouter`.
