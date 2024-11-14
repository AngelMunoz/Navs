(**
---
categoryindex: 0
index: 1
title: Navs
category: Libraries
description: A library for bare bones routing in F# applications
keywords: navigation, routing, url, navigation, navs
---

Navs is a... let's say general purpose routing library, it works over a generic value type to enable flexibility.
That means that you can use it most likely anywhere you need to route something. The main use cases are for Desktop Applications.
But there's nothing that can stop you from using it in games or even in console applications.

## Usage

To use this library you need to do a couple of things:

- Define your routes
- Create a router

From there on, you can use the router to navigate to different parts of your application.

*)

(*** hide ***)
#r "nuget: Navs, 1.0.0-rc-002"

open FSharp.Data.Adaptive
open System
open System.Threading.Tasks
open UrlTemplates.RouteMatcher

(*** show ***)

open Navs
open Navs.Router

module Task =

  let empty = Task.FromResult(()) :> Task

type Page = {
  title: string
  content: string
  onAction: unit -> Task
}

let routes = [
  Route.define<Page>(
    "home",
    "/home",
    fun _ _ -> {
      title = "Home"
      content = "Welcome to the home page"
      onAction = fun () -> Task.empty
    }
  )
  Route.define<Page>(
    "about",
    "/about",
    fun _ _ -> {
      title = "About"
      content = "This is the about page"
      onAction = fun () -> Task.empty
    }
  )
]

let router =
  Router.build<Page>(
    routes,
    fun () -> {
      title = "Splash"
      content = "Loading..."
      onAction = fun () -> Task.empty
    }
  )

(**
At this point we've defined our routes and created a router. The router is ready to be used to navigate to different parts of the application.

However, the router doesn't navigate anywhere by itself, it requires the user to trigger a navigation event. We can provide a `splash` screen to show while we trigger the first navigation.
*)

(*** hide ***)
let view = router.Content |> AVal.force

printfn "Current view: \n\n%A" view
(*** include-output ***)

task {
  let! navigationResult = router.Navigate "/home"

  match navigationResult with
  | Ok() -> printfn "Navigated to home"
  | Error errors -> printfn "Failed to navigate to home: %A" errors
}
(*** include-output ***)
(*** hide ***)
|> Async.AwaitTask
|> Async.RunSynchronously

(**
  The `router.Navigate` function returns a `Task` that can be awaited to get the result of the navigation. The result is a `Result` type that contains the navigation errors if there were any.
  In this case, we're navigating to the `/home` route, and we're printing a message if the navigation was successful or if it failed.

  In order to access the rendered content of the route, you can use the `router.Current` property. This property will contain the rendered content of the current route.
  Or if you're using FSharp.Data.Adaptive, you can use the `router.AcaptiveContent` property to get the rendered content as an adaptive value.
*)
let adaptiveContent () = adaptive {
  let! view = router.Content
  // the view will always be the most up to date view
  match view with
  | ValueSome view ->
    // ... do something with the view ...
    printfn "Current view: \n\n%A" view
    return ()
  | ValueNone ->
    printfn "No view currently"
    // ... do something else ...
    return ()
}
(**
  If you need an observable, you can easily wrap the `router.Content` property in an observable.
*)

// extend the existing AVal module
module AVal =
  let toObservable (value: aval<_>) =
    { new IObservable<_> with
        member _.Subscribe(observer) = value.AddCallback(observer.OnNext)
    }

// subscribe to the router content
router.Content |> AVal.toObservable


(**
 > ***NOTE***: If you're coming from C# and you're looking for the observables you can create an extension method in a simialr fashion, but keep in mind that you can use FSharp.Data.Adaptive from C# as well via the [CSharp.Data.Adaptive](https://www.nuget.org/packages/CSharp.Data.Adaptive) package.
*)

(**
## Async Routes

Routes can be asynchronous, this is useful when you need to fetch data from
an API or a database before rendering the view. However be mindful that the longer it takes to resolve
these async routes, the longer it will take to render the view.
Creating Async Routes is as simple as returning an `Async` or a `Task` from the route handler.

For support cancellation, you can pull the cancellation token from the Async computation you're working with.
*)


let asyncRoute =
  Route.define<Page>(
    "async",
    "/async",
    fun _ _ -> async {
      let! token = Async.CancellationToken
      do! Task.Delay(90, token) |> Async.AwaitTask

      return {
        title = "Async"
        content = "This is an async route"
        onAction = fun () -> Task.empty
      }
    }
  )

(**

Task based routes have access to the cancellation token, so you can support cancellation in your routes.
*)

let taskRoute =
  Route.define<Page>(
    "task",
    "/task",
    fun _ (nav: INavigable<Page>) token -> task {
      do! Task.Delay(90, token)

      return {
        title = "Task"
        content = "This is a task route"
        onAction =
          fun () -> task {
            do! Task.Delay(90)
            nav.Navigate("/home") |> ignore
          }
      }
    }
  )


(**
## The RouteContext object

The `cref:T:Navs.RouteContext` object is a record that contains the following properties:


- `Route` - RAW URL that is being activated.
- `UrlInfo` - An object that contains the segments, query and hash of the URL in a string form.
- `UrlMatch` - An object that contains multiple dictionaries with the parameters that were extracted from the URL either from the url parameters the query string or the hash portion of the URL.

If you wanted to define a route that takes a parameter, and then access that in the route handler, you can do so by using the `RouteContext` object.

*)


Route.define<Page>(
  "param",
  "/param/:id<guid>",
  fun ctx _ ->
    let guid = ctx.urlMatch |> UrlMatch.getFromParams<Guid> "id"

    {
      title = "Param"
      content = $"This is a route with a parameter: {guid}"
      onAction = fun () -> Task.empty
    }
)

(**
## The `INavigable<'View>` and `IRouter<'View>` interface

The ``cref:T:Navs.INavigable`1`` is provided so you can perform `Navigate` and `NavigateByname` operations within your view handlers.
Perhaps when a button is clicked, or when a link is clicked, you can use the `INavigable` interface to navigate to a different route.
*)

Route.define<Page>(
  "param",
  "/param/:id<guid>",
  fun ctx (nav: INavigable<Page>) ->
    let guid = ctx.urlMatch |> UrlMatch.getFromParams<Guid> "id"

    {
      title = "Param"
      content = $"This is a route with a parameter: {guid}"
      onAction =
        fun () -> task {
          match! nav.Navigate("/home") with
          | Ok _ -> ()
          | Error errors -> printfn "Failed to navigate to home: %A" errors
        }
    }
)
(**
The UrlMatch property in the context has algo access to the QueryParams and the Hash of the URL that was matched for this route.

> For more information about extracting parameters from the URL, please refer to the [UrlTemplates Document section](./UrlTemplates.fsx).

### State and StateSnapshot

Sometimes it is useful to know if the router is currently navigating to a route or if it's idle.
You can use the ``cref:M:Navs.INavigable`1.State`` and ``cref:M:Navs.INavigable`1.StateSnapshot`` properties to get the current state of the router.

The `State` property is an adaptive value that will emit the current state of the router.

The `StateSnapshot` property is a property that will return the current state of the router.

*)

let state = router.StateSnapshot

match state with
| NavigationState.Idle -> printfn "The router is idle"
| NavigationState.Navigating -> printfn "The router is navigating"

(**
The ``cref:T:Navs.IRouter`1`` interface is reserved to the router object and it provides a few more properties than the navigable interface.
This is manly because the rotuer is likely to be used like a service in the application while the navigable interface is more of a route tied object.

### Route and RouteSnapshot

The ``cref:M:Navs.IRouter`1.Route`` property is an adaptive value that represents the actual context used by the active route.
while the ``cref:M:Navs.IRouter`1.RouteSnapshot`` property is a stale version of the previous.

*)

let route = router.RouteSnapshot

(*** hide ***)
printfn "Current route: \n\n%A" route
(*** include-output ***)

(**

This property can be useful if you want to make some decisions above the route's handler based on the current route
like showing/hiding a navigation bar or a sidebar.

## Guards

Guards are a way to prevent a route from being activated or deactivated. They run before any navigation is performed, the `Can Activate` guards run first followed by the `Can Deactivate` guards.
If any of these guards return a `false` value, the navigation will not initiate at all and the `NavigationError<T>` will be returned from the `router.Navigate` call.

Guards also have access to the `RouteContext` object, so you can use the `RouteContext` object to make decisions based on the current route. In a similar fashion, remember that
the longer it takes to resolve the guard, the longer it will take to navigate to the route.
*)

asyncRoute
|> Route.canActivateAsync(fun routeContext _ -> async {
  let! token = Async.CancellationToken
  do! Task.Delay(90, token) |> Async.AwaitTask
  // return Continue to allow the navigation
  // return Stop to prevent the navigation
  return Continue
})
|> Route.canDeactivateAsync(fun routeContext _ -> async {
  let! token = Async.CancellationToken
  do! Task.Delay(90, token) |> Async.AwaitTask
  // return Continue to allow the navigation
  // return Stop to prevent the navigation
  return Stop
})
|> Route.canActivateAsync(fun routeContext _ -> async {
  let! token = Async.CancellationToken
  do! Task.Delay(90, token) |> Async.AwaitTask
  // CanActivate guards can also "Re-direct" to a different route
  return Redirect "/home"
})

(**

Route Can Activate Guards can also be used to redirect to a different route, for example you can protect a route and if the user is not authenticated you can redirect them to the login view.

> ***Note***: Can Deactivate guards while accept the "Redirect" result, they will not redirect the user to a different route. It will behave the same as if the guard returned "Stop".

## Caching

The default behavior of the router is to obtain the view from an internal cache if it's available. However, you can change this behavior by using the `Route.cache` function.
Which will make the router always re-execute the route handler when the route is activated.

Even if you cache a route there are certain features that will always execute regardless.

- Parameter parsing and route resolution.
- Check for cancellation at the navigation level.
- Route Guards.

*)

asyncRoute |> Route.cache CacheStrategy.NoCache



(**
The rule of thumb for cachign is:

- [ ] Is the view stateful?
- [ ] Is the user expecting to come back and see the same state in the view?
- [ ] Is the view expensive to render?

If any of the above checks true, then you should consider caching the view

- [ ] Is the view state ephemeral and can be discarded when navigating away?
- [ ] Do you want to avoid stale data any time the route is activated?
- [ ] Do you need to dispose of resources when the route is deactivated?

If any of the above checks true, then you should consider not caching the view.

Now keep in mind these are not golden rules written in stone. By default we cache the views, but you can change this behavior in the case it is not suitable for your application.
*)
