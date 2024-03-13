---
index: 0
title: Creating an adapter.
category: Navs
---

## Creating an adapter.

    [hide]
    #r "nuget: Navs, 1.0.0-beta-006"

Sometimes you may want to create a custom adapter when you know the concrete types (or the interface) that you're targeting with your router and your definitions. This is a guide on how to create an adapter for a custom type.

Let's take a look at how we implemented the Plain Avalonia adapter. Normal Avalonia applications use multiple kind of controls to represent the UI and all of them derive from the `Control` class somewhere on their inheritance chain. We can use this to our advantage and create a custom adapter that works with `Control` instances.

    open Navs
    open Navs.Router

    type AvaloniaRouter(routes: RouteDefinition<Control> seq) =
      inherit Router<Control>(rRouteTracks.fromDefinitions routesoutes)

In this case we've created a custom router that works with `Control` instances. so the user doesn't have to fight a bunch of `<>` brakets all over the place specially from C# where the type inference is not as good as in F#.

For F# then the next thing would be to create a custom `Route` type that works with `Control` instances.

    type Route =

      static member define<'View when 'View :> Control>
        (
          name,
          path,
          handler: RouteContext -> INavigate<Control> -> Async<'View>
        ) : RouteDefinition<Control> =
        Navs.Route.define<Control>(
          name,
          path,
          fun ctx nav -> async {
            // here sadly we have to cast the result to Control
            // because the type hierarchy is solvable by the F# compiler without hints
            // so we have to help it a little bit.
            let! result = handler ctx nav
            return result :> Control
          }
        )

Once that is done, we can convert our route definitions to the custom type and use the custom router.

From

    let router = Router.get<Control>([
      Route.define<Control>("Home", "/", fun ctx _ -> async {
        do! Async.Sleep 90
        return UserControl()
      })
    ])

To

    let router: IRouter<Control> = AvaloniaRouter([
      Route.define("Home", "/", fun ctx _ -> async {
        do! Async.Sleep 90
        return UserControl()
      })
    ])

Yay... less `<>` brackets.

To interop with other languages though it is not required, but it is recommended to also create a `Route` type that takes `Func` instead of F# functions.

    module Interop =

      type Route =
        [<CompiledName "Define">]
        static member inline define
          (
            name,
            path,
            handler: Func<RouteContext, INavigate<Control>, #Control>
          ) =
          Navs.Route.define(name, path, (fun args -> handler.Invoke(args) :> Control))

This will make sure that the route definitions can be created from C# or any other language that can interop with.

    [lang=csharp]
    using Route = Navs.Interop.Route;
    IRouter<Control> =
      new AvaloniaRouter([
        Route.Define("Home", "/", (ctx, _) => {
          return new UserControl();
        })
      ]);

In general what you want to do is to help the compiler solve the correct type from the types that are being used by your users, in a similar sense that's what we do in the FuncUI adapter as well.

    [module=FuncUI]

    type Route =

      static member define
        (
          name,
          path,
          handler: RouteContext -> INavigate<IView> -> Async<#IView>
        ) : RouteDefinition<IView> =
        Navs.Route.define<IView>(
          name,
          path,
          fun ctx nav -> async {
            let! view = handler ctx nav
            return view :> IView
          }
        )

If there are no complex hierarchies involved then just a small wrapper around the generic type is enough to make it easier to use from the language you are targeting.

## `INavigable<T>` and `IRouter<T>`

The `IRouter<T>` interface actually inherits from `INavigable<T>`, these two interfaces are separated for one reason, for you to be able to navigate within your handlers. The `INavigable<T>` interface is contains the `Navigate` and `NavigateByName` methods, whike the `IRouter<T>` interface also contains the current view so you can use it in your UI.

    let router: IRouter<Control> = AvaloniaRouter([
      Route.define("Home", "/", fun ctx (nav: INavigable<Control>) -> async {
        do! Async.Sleep 90
        return
          Button()
            .AddClickHandler(fun _ _ -> async {
              do!
                nav.Navigate("/about")
                |> Async.AwaitTask
                |> Async.Ignore
            }
            |> Async.StartImmediate)
      })
    ])

    do! router.Navigate("/home")

    Window()
      .content(router.Content |> AVal.toBinding)

That is a brief example but it should show the main difference between the two, in any case they're backed by the same mechanisms to make the work effective.
