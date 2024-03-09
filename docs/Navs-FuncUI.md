---
categoryindex: 0
index: 3
title: Navs.FuncUI
category: Libraries
---

    [hide]
    #r "nuget: Navs.Avalonia, 1.0.0-beta-004"

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


let navbar (router: FuncUIRouter) : IView =
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
    (fun _ -> TextBlock.create [ TextBlock.text "Books" ])
  )
  Route.define(
    "guid",
    "/:id<guid>",
    fun (context, _) -> async {
      return
        TextBlock.create [
          match context.UrlMatch.Params.TryGetValue "id" with
          | true, id -> TextBlock.text $"Visited: {id}"
          | false, _ -> TextBlock.text "Guid No GUID"
        ]
    }
  )
]

let appContent (router: FuncUIRouter, navbar: FuncUIRouter -> IView) =
  Component(fun ctx ->

    let currentView = ctx.useRouter router

    DockPanel.create [
      DockPanel.lastChildFill true
      DockPanel.children [ navbar router; currentView.Current ]
    ]
  )
```

## useRouter Hook

The `useRouter` hook is a very simplistic one it takes a `cref:T:Navs.FuncUI.FuncUIRouter` and returns the current view based on the current route.

```fsharp

let appContent (router: FuncUIRouter, navbar: FuncUIRouter -> IView) =
  Component(fun ctx ->
    // The useRouter hook
    let iView = ctx.useRouter router

    DockPanel.create [
      DockPanel.lastChildFill true
      DockPanel.children [ navbar router; iView.Current ]
    ]
  )
```
