# The Navs Family

Welcome to the Navs Family documentation.

This documentation is a work in progress.

This project contains the following libraries:

- [Navs](#Navs)
- [UrlTemplates](#UrlTemplates)
- [Navs.Avalonia](#Navs-Avalonia)
- [Navs.FuncUI](#Navs-FuncUI)

### Navs

Navs is a router-like abstraction inspired by web routers such as vue-router, angular-router and similar projects.

It is primarily a "core" library which you would usually depend on in your own projects, as it is very generic and while F# can be very intelligent about type inference, it tends to produce quite verbose signatures. For more information visit the Navs section in these docs.

- [Navs](./Navs.fsx)

A Compelling Example:

```fsharp

let routes = [
  Route.define<string>("home", "/", (fun _ -> "Home"))
  Route.define<string>("about", "/about", (fun _ -> "About"))
  Route.define<string>(
    "guid",
    "/:id<guid>",
    fun context _ -> async {
      do! Async.Sleep(90)
      return
        match context.UrlMatch.Params.TryGetValue "id" with
        | true, id -> sprintf "Home %A" id
        | false, _ -> "Guid No GUID"
    }
  )
]

let router = Router.get<string>(routes)

router.Content.AddCallback(fun content -> printfn $"%A{content}")

let! result1 = router.navigate("/about")
let! result2 = router.navigate("/home")
let! result3 = router.navigate("/123e4567-e89b-12d3-a456-426614174000")

// "About"
// "Home"
// "Home 123e4567-e89b-12d3-a456-426614174000"

```

### Navs.Avalonia

This project attempts to hide the generics from call sites and offer a few DSLs to make it easier to use Navs in Avalonia applications. This router was designed to be used with Raw Avalonia Control classes however, it will pair very nicely with the [NXUI](https://github.com/wieslawsoltes/NXUI) project, Feel free to check the C# and F# samples in the [Samples](https://github.com/AngelMunoz/Navs/tree/main/samples) folder in the source code repository.

- [Navs.Avalonia](./Navs.Avalonia/index.md)

A Compelling Example:

```fsharp

let routes = [
  Route.define(
    "guid",
    // routes can be typed!
    "/:id<guid>",
    fun context _ -> async {
      // you can pre-load data if you want to
      do! Async.Sleep(90)
      return
        // extract parameters from the URL
        match context.urlMatch |> UrlMatch.getFromParams<guid> "id" with
        | ValueSome id -> TextBlock().text(sprintf "Home %A" id)
        | ValueNone -> TextBlock().text("Guid No GUID")
    }
  )
  // Simpler non-async routes are also supported
  Route.define("books", "/books", (fun _ _ -> TextBlock().text("Books")))
]

let navigate url (router: IRouter<Control>) _ _ =
  task {
    // navigation is asynchronous and returns a result
    // in order to check if the navigation was successful
    let! result = router.Navigate(url)

    match result with
    | Ok _ -> ()
    | Error e -> printfn $"%A{e}"
  }
  |> ignore

let app () =

  let router: IRouter<Control> = AvaloniaRouter(routes, splash = fun _ -> TextBlock().text("Loading..."))

  Window()
    .content(
      DockPanel()
        .lastChildFill(true)
        .children(
          StackPanel()
            .DockTop()
            .OrientationHorizontal()
            .spacing(8)
            .children(
              Button().content("Books").OnClickHandler(navigate "/books" router),
              Button()
                .content("Guid")
                .OnClickHandler(navigate $"/{Guid.NewGuid()}" router)
            ),
          RouterOutlet().router(router)
        )
    )


NXUI.Run(app, "Navs.Avalonia!", Environment.GetCommandLineArgs()) |> ignore
```

### Navs.FuncUI

In a similar Fashion of Navs.Avalonia, this project attempts to provide a smooth API interface for [Avalonia.FuncUI](https://github.com/fsprojects/Avalonia.FuncUI/), you can find a sample in the [Samples](https://github.com/AngelMunoz/Navs/tree/main/samples) folder in the source code repository.

- [Navs.FuncUI](./Navs.FuncUI/index.md)

A Compelling Example:

```fsharp

let routes = [
  Route.define(
    "books",
    "/books",
    (fun _ _ -> TextBlock.create [ TextBlock.text "Books" ])
  )
  Route.define(
    "guid",
    "/:id<guid>",
    fun context _ -> async {
      return
        TextBlock.create [
          match context.urlMatch |> UrlMatch.getFromParams<guid> "id" with
          | ValueSome id -> TextBlock.text $"Visited: {id}"
          | ValueNone -> TextBlock.text "Guid No GUID"
        ]
    }
  )
]

let appContent (router: IRouter<IView>, navbar: IRouter<IView> -> IView) =
  Component(fun ctx ->

    let currentView = ctx.useRouter router

    DockPanel.create [
      DockPanel.lastChildFill true
      DockPanel.children [ navbar router; currentView.Current ]
    ]
  )
```

### UrlTemplates

This is a library for parsing URL-like strings into structured objects. It is used by Navs to parse navigable URLs and URL templates to find if they match.

Currently this library is mainly aimed to be used from F# but if there's interest in using it from C# I can add some more friendly APIs.

- [UrlTemplates](./UrlTemplates.fsx)

A Compelling Example:

```fsharp
open UrlTemplates.RouteMatcher

let template = "/api/v1/profiles/:id<int>?optionalKey<guid>&requiredKey!#hash"
let url = "/api/v1/profiles/2345?requiredKey=2#hash"

match RouteMatcher.matchStrings template url with
| Ok (urlTemplate, urlInfo, urlMatch) ->
  let { Segments = foundParams; Query = queryParams; Hash = foundHash } = urlTemplate
  // foundParams
  // [ Plain ""; Plain "api"; Plain "v1"; Plain "profiles"; Param ("id", "2345");]
  // query
  // [Optional "optionalKeyu", Guid; Required "requiredKey", Int]
  // hash
  // "hash"


  let { Params = urlParams; Query = query; Hash = hash } = urlInfo
  // urlParams
  // [ ""; "api"; "v1"; "profiles"; "2345" ]
  // query
  // [ "optionalKey", String ValueNone; "requiredKey", String ValueSome "2"]
  // hash
  // ValueSome "hash"

  let { Params = foundParams; QueryParams = queryParams; Hash = foundHash } = urlMatch
  // foundParams
  // { { "id", box 2345 } }
  // queryParams
  // { { "requiredKey", box "2" } }
  // foundHash
  // ValueSome "hash"

| Error errors ->
  for e in errors do
    printfn $"%A{e}"
```
