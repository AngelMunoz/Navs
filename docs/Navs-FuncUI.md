---
categoryindex: 0
index: 3
title: Navs.FuncUI
category: Libraries
---

    [hide]
    #r "nuget: Navs.Avalonia, 1.0.0-beta-008"

## Navs.FuncUI

In a similar Fashion of Navs.Avalonia, this project attempts to provide a smooth API interface for [Avalonia.FuncUI](https://github.com/fsprojects/Avalonia.FuncUI/)

## Usage

Avalonia.FuncUI works with the base interface `IView` so any FuncUI control provided by it's DSL can be used with the router.

```fsharp
open Avalonia.FuncUI
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types

open Navs
open Navs.FuncUI
open UrlTemplates.RouteMatcher


let navbar (router: IRouter<IView>) : IView =
  StackPanel.create [
    StackPanel.dock Dock.Top
    StackPanel.orientation Layout.Orientation.Horizontal
    StackPanel.children [
      Button.create [
        Button.content "Books"
        Button.onClick(fun _ -> router.Navigate "/books" |> ignore)
      ]
      Button.create [
        Button.content "Guid"
        Button.onClick(fun _ -> router.Navigate $"/{Guid.NewGuid()}" |> ignore)
      ]
    ]
  ]

let routes = [
  Route.define(
    "books",
    "/books",
    (fun _ _ -> TextBlock.create [ TextBlock.text "Books" ])
  )
  Route.define(
    "guid",
    "/:id<guid>",
    fun context  _ -> async {
      return
        TextBlock.create [
          let id = context.urlMatch |> UrlMatch.getFromParams<Guid> "id"
          match id with
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

## Hooks and extensions

FuncUI provides it's own ways to handle state and side effects. Given the usage we have with FSharp.Data.Adaptive we felt it was necessary to provide a way integrate Adaptive Data with FuncUI's usual way of handling state.

### useAVal Hook

The `cref:M:Navs.FuncUI.IComponentContexExtensions.useAVal` hook converts any adaptive value into a FuncUI `IReadable<'Value>`

    // An external data store for the current component
    let AuthStore = cval {| isAuthenticated = false |}

    Component(fun ctx ->

      let readableVal = ctx.useAVal isAuthenticated

      TextBlock.create [
        TextBlock.text ($"Value: %d{readableVal.Current.IsAuthenticated}")
      ]
    )

In the example above, whenever the adaptive value `isAuthenticated` changes, the `TextBlock` will be updated with the new value. without the need to manually subscribe to the adaptive value.

### useCval Hook

In a similar fashion, the `cref:M:Navs.FuncUI.IComponentContexExtensions.useCval` hook converts any changeable value into a FuncUI `IWritable<'Value>`

    // An external data store for the current component
    let AuthStore = cval {| isAuthenticated = false |}

    Component(fun ctx ->

      let writableVal = ctx.useCval AuthStore

      Button.create [
        math writableVal.Current.IsAuthenticated with
        | true ->
          Button.content ("You're in!")
        | false ->
          Button.content ("Sign in!")
          Button.onClick(fun _ -> writableVal.Set {| isAuthenticated = true |} |> ignore)
      ]
    )

Given that the user clicks the button, the `isAuthenticated` value will be updated and the `Button` will be updated with the new value. in both components consuming the `isAuthenticated` value.

### useRouter Hook

The `useRouter` hook is a very simplistic one it takes a `cref:T:Navs.FuncUI.FuncUIRouter` and returns the current view based on the current route. This hook is available for custom abstractions where the provided router outlet is not enough.

    let appContent (router: IRouter<IView>, navbar: IRouter<IView> -> IView) =
      Component(fun ctx ->
        // The useRouter hook
        let iView = ctx.useRouter router

        DockPanel.create [
          DockPanel.lastChildFill true
          DockPanel.children [ navbar router; iView.Current ]
        ]
      )

## The RouterOutlet

For most of the use cases out there, you don't need to keep a manual linking between the router and the view, the `cref:T:Navs.FuncUI.RouterOutlet` DSl will create a default control that can be used to render the router's current route. It includes a basic page transition and a no content view.

    let windowContent() =
      let router: IRouter<IView> = FuncUIRouter(routes)

      DockPanel.create [
        DockPanel.children [
          Navbar.create router // custom navbar
          // other layout components
          RouterOutlet.create(
            router,
            // provide a fallback view if no content is present
            noContent = TextBlock.create [
              TextBlock.text "No Content"
            ]
          )
        ]
      ]
