---
categoryindex: 0
index: 2
title: Navs.Avalonia
category: Libraries
---

## Navs.Avalonia

The Navs.Avalonia library is a small wrapper over [Navs](./Navs.fsx) that targets the `Control` type in Avalonia Applications.
This means this router can be used with plain Avalonia code without XAML for "code-behind" or "code-first" applications.

This project is meant to be used with [NXUI](https://github.com/wieslawsoltes/NXUI) or similar libnraries that provide a code-first approach to building Avalonia applications. It might be possible though to write a wrapping user control in XAML that uses this router. as it doesn't depend on any specific Avalonia features.

That being said...

## Usage

    [hide]
    #r "nuget: Navs.Avalonia, 1.0.0-beta-003"

Using this library is very similar to using the base Navs library. The main difference is that the `Navs.Avalonia` library provides a less generic versions of the API.

    open NXUI
    open NXUI.FSharp

    open Navs
    open Navs.Avalonia

    let HomeComponent(): Control =
      UserControl()
        .content(
          StackPanel()
            .children(
              TextBlock.text("Home")
              // ... other controls ...
            )
        )

    let AboutComponent(): Control =
      UserControl()
        .content(
          StackPanel()
            .children(
              TextBlock.text("About")
              // ... other controls ...
            )
        )

    let routes = [
      Route.define("home", "/home", fun _ -> HomeComponent())
      Route.define("about", "/about", fun _ -> AboutComponent())
    ]

    let router = AvaloniaRouter(routes)

    let app =
      Window()
        .content(router.Content.ToBinding(), BindingMode.OneWay)

From there you can use the `router` to navigate between the different components and any other usages you have with the base Navs library.
