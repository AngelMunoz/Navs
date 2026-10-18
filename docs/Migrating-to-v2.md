# Migrating from v1 to v2

`2.0.0` changes the router surface and adds C# facades. This page contains the edits your code needs; the package IDs do not change.

The snippets show the v1 code on the left side of each section and its v2 replacement after it.

## Packages

| Package | Change |
|---|---|
| `Navs` | Surface changes described on this page. It no longer references `FSharp.Data.Adaptive`. |
| `UrlTemplates` | Parses with XParsec instead of FParsec, and decodes URL values. |
| `Navs.Avalonia`, `Navs.FuncUI`, `Navs.Terminal.Gui` | Adapters are render-only and take core `RouteDefinition` values. |
| `Navs.CSharp`, `UrlTemplates.CSharp` | New. C# callers no longer need the F# interop namespaces. |

If you referenced `FParsec` only because `UrlTemplates` brought it in, you can remove that reference.

## Route definitions

`Route.define` keeps its three overloads (sync, task, async). What changes is the record: in v2 its fields are internal, so you modify routes with the `Route` functions instead of record update syntax.

v1:

```fsharp
open Navs

let counter =
  { Route.define(
      "counter",
      "/counter?count<int>",
      fun context _ ->
        let initial = defaultValueArg (context.getParam<int> "count") 0
        renderCounter initial
    ) with
      cacheStrategy = CacheStrategy.Cache }
```

v2:

```fsharp
open Navs
open Navs.Router

let counter =
  Route.define(
    "counter",
    "/counter?count<int>",
    fun context _ ->
      let initial =
        context.GetParam<int>("count") |> ValueOption.defaultValue 0

      renderCounter initial
  )
  |> Route.cache(CachePolicy.Cache(maxEntries = 32, ttl = None))
```

The modifiers available in v2 are `Route.cache`, `Route.priority`, `Route.redirect`, `Route.meta`, `Route.canActivate`/`canActivateTask`/`canActivateAsync` and the three `canDeactivate` variants.

## Router creation

`Router.build` returned a router directly. `Router.create` and `Router.createDefault` validate the route table and return a `Result`.

v1:

```fsharp
let router = Router.build routes
```

v2:

```fsharp
let router =
  match Router.createDefault routes with
  | Ok router -> router
  | Error errors -> invalidOp $"Invalid routes: %A{errors}"
```

Use `Router.create` when you need a custom history, splash view, global guards, cache policy, concurrency policy, same-URL policy or redirect limit:

```fsharp
let router =
  Router.create {
    Routes = routes
    History = InMemoryHistory()
    Splash = None
    Guards = []
    Cache = NoCache
    Concurrency = LatestWins
    SameUrl = Ignore
    MaxRedirects = 5
    Logger = None
  }
```

Adapter constructors still take the route list. `AvaloniaRouter`, `FuncUIRouter` and `TerminalGuiRouter` keep their signatures and throw when the route table is invalid.

## Navigation

`Navigate`, `NavigateByName`, `Back` and `Forward` return a `CancellableTask` instead of a `Task`, and they return a `NavigationResult` instead of `unit`. Pass the token when you invoke the task.

v1:

```fsharp
let result = router.Navigate("/about", cancellationToken).GetAwaiter().GetResult()

match result with
| Ok () -> printfn "done"
| Error error -> printfn $"%A{error}"
```

v2:

```fsharp
let result =
  router.Navigate("/about") cancellationToken
  |> fun task -> task.GetAwaiter().GetResult()

match result with
| Ok navigation ->
  printfn $"active route: {navigation.Route.Path}"
  printfn $"redirects: %A{navigation.Redirects}"
| Error error -> printfn $"%A{error}"
```

`NavigateByName` now takes the parameters positionally. The token is passed when the task is invoked:

```fsharp
router.NavigateByName("user", readOnlyDict [ "id", box 42 ]) cancellationToken
```

## Navigation errors

`NavigationError` is no longer generic, and its cases changed.

| v1 | v2 |
|---|---|
| `SameRouteNavigation` | Removed. Same-URL navigation succeeds; history behavior is controlled by `SameUrlPolicy`. |
| `NavigationCancelled` | `Cancelled` |
| `RouteNotFound url` | `RouteNotFound url` |
| `NavigationFailed message` | `ActivationFailed` or `HistoryFailed`, depending on the stage that failed |
| `CantDeactivate route` | `GuardStopped route` |
| `CantActivate route` | `GuardStopped route` |
| `GuardRedirect url` | Removed. Redirects resolve inside `Navigate`; a cycle reports `RedirectLoop chain`. |

The v2 cases also include `InvalidTarget`, `GuardFailed`, `RedirectLoop` and `Superseded`.

## Reading router state

The adaptive values are gone. `Snapshot` returns state, active route, content and history as one value, and `Changed` publishes it whenever it changes.

v1:

```fsharp
let state = router.StateSnapshot
let route = router.RouteSnapshot
let content = router.ContentSnapshot

// v1 exposed these as aval<_> values for the adapters to observe
let contentAdaptive = router.Content
```

v2:

```fsharp
let snapshot = router.Snapshot

snapshot.State
snapshot.Route
snapshot.Content
snapshot.History

router.Changed.Add(fun snapshot -> render snapshot.Content)
```

If you consumed the `aval` values directly, replace them with `Snapshot` plus a subscription to `Changed`.

## RouteContext

The record fields are now PascalCase, and the parameter helpers are members of the record.

v1:

```fsharp
let id = context.getParam<int> "id"
let values = context.getParamSequence<string> "status"
let path = context.path
let match = context.urlMatch
```

v2:

```fsharp
let id = context.GetParam<int>("id")
let values = context.GetParamSequence<string>("status")
let path = context.Path
let urlMatch = context.UrlMatch
```

`GetParam` still returns `'T voption`, and `AddDisposable` keeps its behavior.

## Guards

The registration functions are the same. `GuardResponse` is now `GuardResult` and gains a `Cancel` case.

v1:

```fsharp
let guarded =
  route
  |> Route.canActivate (fun _ _ -> GuardResponse.Continue)
  |> Route.canDeactivate (fun _ _ -> GuardResponse.Redirect "/login")
```

v2:

```fsharp
let guarded =
  route
  |> Route.canActivate (fun _ _ -> GuardResult.Continue)
  |> Route.canDeactivate (fun _ _ -> GuardResult.Redirect "/login")
```

Evaluation order is now defined: deactivation guards run before activation guards, and guards within a route run in registration order. Redirects are bounded by `MaxRedirects` (5 by default).

## Cache

`CacheStrategy` is now `CachePolicy`, and the cache entry takes a size limit and an optional lifetime.

v1:

```fsharp
let cached = route |> Route.cache CacheStrategy.Cache
```

v2:

```fsharp
let cached =
  route
  |> Route.cache(CachePolicy.Cache(maxEntries = 16, ttl = Some(TimeSpan.FromMinutes 5.)))
```

`NoCache` remains the default. `router.ClearCache()` drops every cached view. Cache hits still run guards.

## History and links

Both are new on `IRouter`:

```fsharp
router.History.CanGoBack
router.History.CanGoForward
router.History.Current
router.Back() cancellationToken
router.Forward() cancellationToken

router.Links.TryBuild("user", readOnlyDict [ "id", box 42 ])   // Result<string, string>
router.Links.IsActive("user", readOnlyDict [ "id", box 42 ])
```

`HistorySnapshot.Entries` is an `IReadOnlyList<HistoryEntry>` instead of an F# list.

## UrlTemplates

The public API is unchanged. Two behaviors changed:

1. Path, query and hash values are percent-decoded, and `+` becomes a space in query values.
2. Malformed templates and URLs report the offending character and its index instead of the parser's expectation list.

```fsharp
match UrlParser.ofString "/search?q=hello+world" with
| Ok info ->
  // v1: "hello+world"
  // v2: "hello world"
  printfn $"%A{info.Query}"
| Error message ->
  // v1: "Error in Ln: 1 Col: 6 ... Expecting: ..."
  // v2: "unexpected '%' at index 5"
  printfn "%s" message
```

If you asserted on the error text, update the assertion; the new message is stable across parser engines.

## Adapters

### Navs.Avalonia

The router constructor keeps its shape. The outlet no longer takes a router through an extension method, and the `Routes` control is gone.

v1:

```fsharp
let router = AvaloniaRouter(routes, splash = splash, logger = logger)
let outlet = RouterOutlet().router(router)
```

v2:

```fsharp
let router = AvaloniaRouter(routes, splash = splash, logger = logger) :> IRouter<Control>

let outlet = RouterOutlet()
outlet.Router <- Some router
outlet.NoContent <- Some(block "No content")
```

### Navs.FuncUI

`FuncUIRouter(routes)` and `RouterOutlet.create(router, noContent = ...)` keep their shape. Routes must be core `RouteDefinition<IView>` values created with `Route.define`.

### Navs.Terminal.Gui

`TerminalGuiRouter(routes, ?splash, ?logger)` and `RouterOutlet(router, ?initialUri, ?logger)` take core `RouteDefinition<Window>` values. The adapter no longer exposes its own route wrapper.

## C#

v1 required `Navs.Interop`, the adapter `Interop.Route` types and adaptive bindings. v2 ships two facades.

v1:

```csharp
using Navs.Interop;
using Route = Navs.Avalonia.Interop.Route;
```

v2:

```csharp
using Navs.CSharp;

var router = Router.Create(
    Route.Define<string>("home", "/", (context, navigable) => "Home"),
    Route.Define<string>("user", "/users/:id<int>", (context, navigable) => context.Path));

var outcome = await router.NavigateAsync("/users/42");

if (outcome.IsSuccess)
{
    outcome.Result!.Route.TryGetParam<int>("id", out var id);
}
```

`Navs.CSharp` reports failures through `NavigationOutcome` and `NavigationErrorCode`, and adds `TryBuildLink`, `IsActive`, `TryGetParam<T>` and `GetParamValues<T>`.

If you use `UrlTemplates` from C#, use `UrlTemplates.CSharp`:

```csharp
using UrlTemplates.CSharp;

if (UrlTemplate.TryParse("/users/:id<int>", out var template, out var parseError)
    && RouteMatcher.TryMatch(template!, "/users/42", out var match, out var errors))
{
    match!.TryGetPathParam<int>("id", out var id);
}
```

## Checklist

- Replace `Router.build` with `Router.createDefault` or `Router.create` and handle the `Result`.
- Invoke navigations with the token; read `NavigationResult` instead of `unit`.
- Update `NavigationError` cases with the table above.
- Replace `StateSnapshot`, `RouteSnapshot` and `ContentSnapshot` with `Snapshot`; subscribe to `Changed`.
- Rename `context.path`, `urlMatch`, `urlInfo`, `addDisposable`, `getParam` and `getParamSequence`.
- Replace `GuardResponse` with `GuardResult`.
- Replace `CacheStrategy` with `CachePolicy` and set the size limit and lifetime.
- Update the Avalonia outlet to `RouterOutlet()` plus `Router <- Some router`.
- Update UrlTemplates assertions: values are decoded and error text changed.
- Switch C# callers to `Navs.CSharp` and `UrlTemplates.CSharp`.
