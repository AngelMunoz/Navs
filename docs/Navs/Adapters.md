---
index: 0
title: Creating an adapter
category: Navs
---

# Creating an adapter

An adapter binds the core router to a concrete view type and renders the active view. It should not implement routing, guards, caching or history.

## Wrap the core router

```fsharp
open System.Threading
open Navs
open Navs.Router

type SampleRouter(routes: RouteDefinition<MyView> seq) =
    let router =
        match Router.createDefault(routes) with
        | Ok value -> value
        | Error errors -> invalidOp $"Invalid routes: %A{errors}"

    interface IRouter<MyView> with
        member _.Snapshot = router.Snapshot
        member _.Changed = router.Changed
        member _.History = router.History
        member _.Links = router.Links
        member _.Navigate(url) = router.Navigate(url)
        member _.NavigateByName(name, parameters) = router.NavigateByName(name, parameters)
        member _.Back() = router.Back()
        member _.Forward() = router.Forward()
```

`IRouter<'View>` inherits `INavigable`, which contains only navigation. The router-side snapshot and content belong to `IRouter<'View>`.

## Render the outlet

An outlet subscribes to `Changed` and renders `Snapshot.Content`:

```fsharp
let outlet (router: IRouter<MyView>) =
    router.Changed.Add(fun snapshot ->
        match snapshot.Content with
        | ValueSome view -> render view
        | ValueNone -> renderEmpty ()
    )
```

Keep subscriptions disposable and render on the UI thread required by the framework.

## Register routes

Do not create an adapter-specific route type. Use the core DSL:

```fsharp
let routes = [
    Route.define<MyView>("home", "/", fun _ _ -> homeView ())
    Route.define<MyView>("about", "/about", fun _ _ -> aboutView ())
]
```

For C#, use `Navs.CSharp` and `NavigationOutcome`. The adapter assembly should expose only the view-specific router and outlet.

## What the adapter must not do

- Parse or rank route patterns.
- Implement guards, redirects, caching or history.
- Keep its own navigation state.
- Expose F# `Result`, `Option` or `ValueTask` in a C#-facing API.
