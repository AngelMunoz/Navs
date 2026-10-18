# The Navs Family

Navs is a router family for .NET applications. The core library is UI-agnostic; the adapters bind it to Avalonia, FuncUI and Terminal.Gui.

- `Navs` — core router, route table, guards, history and cache.
- `UrlTemplates` — URL and route-pattern parsing.
- `Navs.Avalonia` — Avalonia controls.
- `Navs.FuncUI` — Avalonia.FuncUI components.
- `Navs.Terminal.Gui` — Terminal.Gui controls.
- `Navs.CSharp` — C#-friendly facade.

## Navs (F#)

```fsharp
open System
open System.Threading
open Navs
open Navs.Router

let routes = [
    Route.define<string>("home", "/", fun _ _ -> "Home")
    Route.define<string>("about", "/about", fun _ _ -> "About")
    Route.define<string>(
        "guid",
        "/:id<guid>",
        fun context _ -> async {
            do! Async.Sleep 90
            return
                match context.GetParam<Guid>("id") with
                | ValueSome id -> $"Guid {id}"
                | ValueNone -> "No GUID"
        }
    )
]

let router =
    match Router.createDefault(routes) with
    | Ok router -> router
    | Error errors -> invalidOp $"Invalid routes: %A{errors}"

let result =
    router.Navigate("/about") CancellationToken.None
    |> fun task -> task.GetAwaiter().GetResult()

match result with
| Ok _ -> printfn "Current view: %A" router.Snapshot.Content
| Error error -> printfn "Navigation failed: %A" error
```

`Navigate` returns a `CancellableTask<Result<...>>`. Pass the token when you invoke it. The router exposes its current state through `router.Snapshot` and publishes changes through `router.Changed`.

## Navs (C#)

```csharp
using Navs.CSharp;

var routes = new[]
{
    Route.Define<string>("home", "/", (context, navigable) => "Home"),
    Route.Define<string>("about", "/about", (context, navigable) => "About")
};

var router = Router.Create(routes);
var outcome = await router.NavigateAsync("/about");

if (outcome.IsSuccess)
{
    Console.WriteLine(router.Snapshot.Content);
}
else
{
    Console.WriteLine(outcome.Error?.Code);
}
```

C# receives `NavigationOutcome`; it does not need to handle F# `Result` or `ValueOption`.

## Navs.Avalonia

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

let router = AvaloniaRouter(routes) :> IRouter<Control>

let outlet = RouterOutlet()
outlet.Router <- Some router

router.Navigate("/") CancellationToken.None |> ignore
```

## Navs.FuncUI

```fsharp
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Navs
open Navs.Router
open Navs.FuncUI

let routes = [
    Route.define<IView>("books", "/books", fun _ _ -> TextBlock.create [])
    Route.define<IView>("about", "/about", fun _ _ -> TextBlock.create [])
]

let router = FuncUIRouter(routes) :> IRouter<IView>
let outlet = RouterOutlet.create(router)
```

## Navs.Terminal.Gui

```fsharp
open Terminal.Gui.Views
open Navs
open Navs.Router
open Navs.Terminal.Gui

let routes = [
    Route.define<Window>("home", "/", fun _ _ -> Window())
    Route.define<Window>("about", "/about", fun _ _ -> Window())
]

let router = TerminalGuiRouter(routes) :> IRouter<Window>
let outlet = RouterOutlet(router)
```

## UrlTemplates

`UrlTemplates` parses URL templates and URLs. `Navs` compiles route patterns at router creation, so matching does not reparse templates on every navigation.

```fsharp
open UrlTemplates.RouteMatcher

let template = "/api/v1/profiles/:id<int>?optionalKey<guid>&requiredKey!#hash"
let url = "/api/v1/profiles/2345?requiredKey=2#hash"

match RouteMatcher.matchStrings template url with
| Ok (parsedTemplate, urlInfo, urlMatch) ->
    printfn "%A" parsedTemplate
    printfn "%A" urlInfo
    printfn "%A" urlMatch
| Error errors -> printfn "%A" errors
```
