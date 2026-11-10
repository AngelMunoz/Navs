# Navs.Goo

`Navs.Goo` binds the core router to Goo `Blob` values.

## Define routes

```fsharp
open Goo
open Navs
open Navs.Router
open Navs.Goo

let routes = [
    Route.define<Blob>("home", "/", fun _ _ -> Text(Content = "Home") :> Blob)
    Route.define<Blob>("about", "/about", fun _ _ -> Text(Content = "About") :> Blob)
]
```

A route returns a fresh `Blob` tree for each navigation. A route that needs state returns a `Cell.Mount` blob with a factory and a key. Goo retains the mounted Cell and disposes it when the blob leaves the tree. Do not return a `Cell` instance as the route view, and do not register a mounted Cell in `RouteContext.Disposables`.

## Create the router

```fsharp
let router = GooRouter(routes) :> IRouter<Blob>
```

The constructor accepts an optional splash factory:

```fsharp
let router =
    GooRouter(
        routes,
        splash = System.Func<Blob>(fun () -> Text(Content = "Loading...") :> Blob)
    ) :> IRouter<Blob>
```

The adapter uses the core defaults of the other adapters: `LatestWins` concurrency, same-URL `Ignore`, `NoCache`, and at most five redirects.

## Render the outlet

The outlet is a Goo `Cell`. Use it as the window root, then give it the window so change notices from other threads can reach the UI thread with `Window.Post`.

```fsharp
let outlet = new RouterOutlet(initialUri = "/")
outlet.Router <- Some router

let window = Window(Title = "Navs.Goo", Width = 720, Height = 480, Root = outlet)
outlet.Window <- Some window
window.Run()
```

With an initial URI, the outlet posts the first navigation when both the router and the window are set. Without a window, the first navigation stays pending until a window is set. Host code can also navigate on its own and skip the initial URI:

```fsharp
router.Navigate("/") System.Threading.CancellationToken.None |> ignore
```

The outlet subscribes to `IRouter.Changed` when its `Router` is set. On the UI thread it rebuilds itself. From another thread it posts the rebuild to the window. It drops the rebuild when `Window.Post` reports that the window has started to close. Set `NoContent` to a `Blob` to render before the first navigation completes.

## Navigate

```fsharp
router.Navigate("/about") System.Threading.CancellationToken.None |> ignore
```

Navigation returns `CancellableTask<Result<...>>`. Invoke it with the token. Calls from Goo input callbacks already run on the UI thread. Calls from other threads must go through the window or through a router change that the outlet can post back.

## Text in native builds

`GooRouter` formats route table errors by matching each error case and using its plain fields. The outlet formats navigation errors the same way before it logs them. Error text keeps its fields in Native AOT builds instead of printing only the case name.

## Sample

See `samples/GooSample` for a full app with Home, Counter, and About routes. Each page takes data as one struct Props value, so Props keeps equality. Each page takes its callbacks as direct function arguments. A page records callbacks in a record only when the count grows past three. Cell state stays in local mutable values inside the module `create` factory. The sample attaches the Goo system theme with `Theme.Attach(window)` and reads theme tokens inside each Cell build, so pages follow the system light or dark palette.

Run it with:

```sh
dotnet run --project samples/GooSample
```

## Widgets

`Goo.Widgets` navigation controls such as `TabBar` and `NavigationRail` are controlled components. The app shell owns the selected ID. Map the active route name to `SelectedId`, and map `OnSelect` to `NavigateByName` or to a URL from `router.Links.TryBuild`. The adapter adds no widget package of its own.
