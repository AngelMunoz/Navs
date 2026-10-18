(**
---
categoryindex: 0
index: 1
title: Navs
category: Libraries
description: A UI-agnostic router for F# applications
keywords: navigation, routing, url, guards, history
---

# Navs

`Navs` is a UI-agnostic router. Routes are flat, the view type is generic, and the adapters bind the router to Avalonia, FuncUI or Terminal.Gui.

The core pieces are:

- `Route.define` for route registration.
- `Router.create` or `Router.createDefault` to compile and validate routes.
- `INavigable` for navigation from handlers.
- `IRouter<'View>` for the snapshot and the current content.
- `IHistory` for back and forward navigation.
- `IRouteLinks` for URL generation and active links.

## Define routes

```fsharp
open System
open System.Threading
open Navs
open Navs.Router

let routes = [
    Route.define<string>("home", "/", fun _ _ -> "Home")
    Route.define<string>("about", "/about", fun _ _ -> "About")
    Route.define<string>(
        "user",
        "/users/:id<int>",
        fun context _ -> async {
            let id = context.GetParam<int>("id") |> ValueOption.defaultValue 0
            return $"User {id}"
        }
    )
]
```

`Route.define` accepts synchronous, `Task` with token, `Async`, and `cancellableTask` handlers. For `Task` and `Async`, add the view type explicitly:

```fsharp
Route.define<string>(
    "user",
    "/users/:id<int>",
    fun context _ token -> task {
        let! _ = Async.Sleep(50) |> Async.StartAsTask
        return $"User {context.GetParam<int>("id")}"
    }
)
```

## Create the router

```fsharp
let router =
    match Router.createDefault(routes) with
    | Ok router -> router
    | Error errors -> invalidOp $"Invalid routes: %A{errors}"
```

For splash, guards, cache or concurrency policy, use `Router.create`:

```fsharp
let config = {
    Routes = routes
    History = InMemoryHistory()
    Splash = Some "Loading..."
    Guards = []
    Cache = NoCache
    Concurrency = LatestWins
    SameUrl = Ignore
    MaxRedirects = 5
    Logger = None
}

let router =
    match Router.create config with
    | Ok router -> router
    | Error errors -> invalidOp $"Invalid routes: %A{errors}"
```

`Router.create` parses and validates the route table once. Duplicate names, invalid patterns and empty tables return `Error` instead of failing during navigation.

## Navigate

`Navigate` returns `CancellableTask<Result<NavigationResult, NavigationError>>`. Invoke it with a token:

```fsharp
let result =
    router.Navigate("/about") CancellationToken.None
    |> fun task -> task.GetAwaiter().GetResult()

match result with
| Ok navigation -> printfn "Navigated to %s" navigation.Route.Path
| Error error -> printfn "Navigation failed: %A" error
```

`NavigateByName` accepts a route name and a parameter dictionary:

```fsharp
open System.Collections.Generic

let parameters = Dictionary<string, obj>()
parameters["id"] <- 42 :> obj

let result = router.NavigateByName("user", parameters) CancellationToken.None
```

## Observe the router

```fsharp
let snapshot = router.Snapshot
printfn "State: %A" snapshot.State
printfn "Content: %A" snapshot.Content
printfn "Route: %A" snapshot.Route

router.Changed.Add(fun value ->
    printfn "State changed to %A" value.State
)
```

## Guards

Guards run before content resolution. Deactivation guards run before activation guards.

```fsharp
let requireAuthentication : SyncGuard =
    fun _ _ -> Continue

let confirmLeave : SyncGuard =
    fun _ _ -> Stop

let protectedRoute =
    Route.define<string>("settings", "/settings", fun _ _ -> "Settings")
    |> Route.canActivate requireAuthentication
    |> Route.canDeactivate confirmLeave
```

Async, task and cancellable guards are also available:

```fsharp
Route.define<string>("secure", "/secure", fun _ _ -> "Secure")
|> Route.canActivateTask(fun _ _ token -> task {
    let! allowed = checkPermission token
    return if allowed then Continue else Stop
})
```

`Redirect` restarts navigation for another URL. `Stop` returns a typed `GuardStopped` error and keeps the previous view.

## Redirects

```fsharp
Route.define<string>("old", "/old", fun _ _ -> "Old")
|> Route.redirect(NavigationTarget.Url "/new")
```

Guard redirects use the same mechanism and are bounded by `MaxRedirects`.

## History

```fsharp
router.Back() CancellationToken.None
|> fun task -> task.GetAwaiter().GetResult()
|> ignore

router.Forward() CancellationToken.None
|> fun task -> task.GetAwaiter().GetResult()
|> ignore

let history = router.History.Snapshot
printfn "Index: %d" history.Index
printfn "Entries: %A" history.Entries
```

## Cache

The default is `NoCache`. Enable caching per route:

```fsharp
Route.define<string>("books", "/books", fun _ _ -> "Books")
|> Route.cache(Cache(maxEntries = 100, ttl = Some(TimeSpan.FromMinutes 5.)))
```

Cached views still run guards. Entries expire by TTL and are evicted when the cache reaches its limit.

## Links

```fsharp
let parameters = Dictionary<string, obj>()
parameters["id"] <- 42 :> obj

match router.Links.TryBuild("user", parameters) with
| Ok url -> printfn "URL: %s" url
| Error error -> printfn "Link failed: %s" error

let active = router.Links.IsActive("user", parameters)
```

## Adapters

- [Navs.Avalonia](./Navs-Avalonia.md)
- [Navs.FuncUI](./Navs-FuncUI.md)
- [Navs.Terminal.Gui](./Navs-Terminal.Gui.md)

For C#, use `Navs.CSharp` and `NavigationOutcome`. See the repository README for a complete example.
*)
