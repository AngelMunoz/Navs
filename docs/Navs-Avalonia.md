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
    #r "nuget: Navs.Avalonia, 1.0.0-rc-002"
    #r "nuget: FSharp.Data.Adaptive, 1.2.14"

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

    let router: IRouter<Control> = AvaloniaRouter(routes)

    let app =
      Window()
        .content(
          DockPanel()
            .lastChildFill(true)
            .children(
              navbar().DockTop() // or any othe component there,
              // use the router outlet
              RouterOutlet().router(router)
            )
        )

From there you can use the `router` to navigate between the different components and any other usages you have with the base Navs library.

## The RouterOutlet

The `RouterOutlet` is a control that will render the current component based on the current route. It also provides page transitions so you can have a smooth experience when navigating between pages.

The `cref:T:Navs.Avalonia.RouterOutlet` control provides three main properties:

- `cref:M:Navs.Avalonia.RouterOutlet.Router` - The router to use to render the current component.
- `cref:M:Navs.Avalonia.RouterOutlet.PageTransition` - The transition to use when the router content changes.
- `cref:M:Navs.Avalonia.RouterOutlet.NoContent` - The content to render when the router content is `None` which can be when the navigation fails or the route is not found.

The outlet is built to be used like any other Avalonia control so you should be able to do things like:

    RouterOutlet()
      .router(router)
      .PageTransition(SlideInTransition())

or in case you're using XAML and are interested in this project

```xml
<RouterOutlet
  Router="{Binding Router}"
  PageTransition="{Binding PageTransition}"
  NoContent="{Binding NoContent}">
</RouterOutlet>
```

Please keep in mind that if you try to modify the `Content` property of the `RouterOutlet` it most certainly will break the outlet's functionality.

## Adaptive Data

If you're going down the Observable route (which lends itslef quite well with Avalonia) you should totally check out [FSharp.Control.Reactive](https://www.nuget.org/packages/FSharp.Control.Reactive) which includes a lot of functionality to work with them.

But internally we use [FSharp.Data.Adaptive](https://www.nuget.org/packages/FSharp.Data.Adaptive) and also have added a few handy extensions to the `Navs.Avalonia` library to make it easier to work with Adaptive Data.

    open FSharp.Data.Adaptive
    open Navs.Avalonia

    let HomeComponent _ _ : Control =
      // create local state
      let counter, setCounter = AVal.useState 0

      UserControl()
        .content(
          StackPanel()
            .children(
              TextBlock()
                .text(
                  // using F# Computation Expressions
                  adaptive {
                    let! counter = counter
                    let double = counter * 2
                    return $"Counter: %d{counter} and double: %d{double}"
                  }
                  |> AVal.toBinding
                ),
              TextBlock()
                .text(
                  // using the AVal.map function
                  counter
                  |> AVal.map(fun counter ->
                    let triple = counter * 3
                    $"Counter: %d{counter} and triple: %d{triple}"
                  )
                  |> AVal.toBinding
                )
              Button()
                .content("Increment")
                .OnClickHandler(fun _ _ ->
                  let currentValue = counter.getValue()
                  // set local state
                  setCounter (currentValue + 1))

            )
        )


    let routes = [
      Route.define("home", "/home", HomeComponent)
    ]

    let router: IRouter<Control> = AvaloniaRouter(routes)

    let app =
      Window()
        .content(router.Content |> AVal.toBinding, BindingMode.OneWay)

F# Adaptive is a reactive model that can be more efficient than standard observables, if you come from the web ecosystem then you might have heard about "signals" which is a very similar concept.

Granular and cacheable updates are the main features of Adaptive Data.

For shared state between components you can pass down the adaptive data and consume it in the child components.

    let siblingA(value: aval<int>) =
      TextBlock()
        .text(
          value
          |> AVal.map(fun v -> $"Sibling A: %d{v}")
          |> AVal.toBinding
        )

    let siblingB(value: aval<int>) =
      TextBlock()
        .text(
          value
          |> AVal.map(fun v -> $"Sibling B: %d{v}")
          |> AVal.toBinding
        )

    let siblingC(value: cval<int>) =
      StackPanel()
        .OrientationHorizontal()
        .spacing(10)
        .children(
          TextBlock()
            .text(
              value
              |> AVal.map(fun v -> $"Sibling C: %d{v}")
              |> AVal.toBinding
            ),
          Button()
            .content("Increment")
            .OnClickHandler(fun _ _ ->
              value.setValue(fun v -> v + 1)
            )
        )

    let parent() =
      let sharedValue = cval 0
      StackPanel()
        .spacing(4)
        .children(
          siblingA(sharedValue)
          siblingB(sharedValue)
          siblingC(sharedValue)
        )

In the above example, `siblingA`, `siblingB`, and `siblingC` are all consuming the same `sharedValue` but the first two are using `aval` which is basically a read-only interface. the `siblingC` is using `cval` which is a read-write interface.

And although this might look like "double-way binding" it's not, the `cval` emits updates in a uni-directional way, the adaptive values depending on it will cascade updates rather than mutating the current in a "listener" fashion.

An alternative of course would be to hoist the updates to the parent and all of the sibling share a read-only interface.

    let siblingC(value: aval<int>, onIncrement) =
      StackPanel()
        .OrientationHorizontal()
        .spacing(10)
        .children(
          TextBlock()
            .text(
              value
              |> AVal.map(fun v -> $"Sibling C: %d{v}")
              |> AVal.toBinding
            ),
          Button()
            .content("Increment")
            .OnClickHandler(fun _ _ -> onIncrement())
        )

    let parent() =
      let sharedValue = cval 0

      let onIncrement () =
        sharedValue.setValue(fun v -> v + 1)

      StackPanel()
        .spacing(4)
        .children(
          siblingA(sharedValue)
          siblingB(sharedValue)
          siblingC(sharedValue, onIncrement)
        )

Hoisting events is a common pattern and tends to be the most flexible way to handle updates, changeable values can act also like stores however, so they can be passed around without much issue as updates are always predictable and transactional.

For cases where you might want to _bind_ the value to a control and make changes propagate automatically, you can use `changeable values` which are adaptive values that can be set directly.

    let myTextBox(value: cval<string>) =
      // requires open Navs.Avalonia
      TextBox()
        .text(value |> AVal.toBinding)
        .OnTextChangedHandler(fun sender _ -> sender.Text |> AVal.setValue value)

    let parent() =
      let sharedValue = cval "Hello"

> **_Note_**: For double way binding we're using the `toBinding` function available in the `CVal` module rather than `AVal.toBinding` which is read-only.

      StackPanel()
        .spacing(4)
        .children(
          myTextBox(sharedValue)
          TextBlock()
            .text(
              sharedValue
              |> AVal.map(fun v -> $"Shared Value: %s{v}")
              |> AVal.toBinding
            )
        )

In that example, the `TextBox` will automatically update the `sharedValue` when the user types in it, and the `TextBlock` will update when the `sharedValue` changes.
This can be very useful for cases where you want to share information that could be updated from multiple components in a reactive and functional way.
