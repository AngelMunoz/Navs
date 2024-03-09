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
#r "nuget: Navs, 1.0.0-beta-004"

open FSharp.Data.Adaptive
open System
open System.Threading
open System.Threading.Tasks
open UrlTemplates.RouteMatcher

(*** show ***)

open Navs
open Navs.Router

type Page = {
  title: string
  content: string
  onAction: (unit -> Task<unit>) option
}

let routes = [
  Route.define<Page>(
    "home",
    "/home",
    fun _ -> {
      title = "Home"
      content = "Welcome to the home page"
      onAction = None
    }
  )
  Route.define<Page>(
    "about",
    "/about",
    fun _ -> {
      title = "About"
      content = "This is the about page"
      onAction = None
    }
  )
]

let router =
  Router<Page>(
    RouteTracks.fromDefinitions routes,
    splash =
      fun nav -> {
        title = "Splash"
        content = "Loading..."
        onAction =
          Some
          <| fun () -> task {
            do! Task.Delay(500)
            nav.Navigate "/home" |> ignore
          }
      }
  )

(**
At this point we've defined our routes and created a router. The router is ready to be used to navigate to different parts of the application.

However, the router doesn't navigate anywhere by itself, it requires the user to trigger a navigation event. We can provide a `splash` screen to show while we trigger the first navigation.
*)

(*** hide ***)
let view = router.AdaptiveContent |> AVal.force

printfn "Current view: \n\n%A" view.Value
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
  let! view = router.AdaptiveContent
  // the view will always be the most up to date view
  match view with
  | Some view ->
    // ... do something with the view ...
    printfn "Current view: \n\n%A" view
    return ()
  | None ->
    printfn "No view currently"
    // ... do something else ...
    return ()
}
(**
  Or in the case of the observable
*)

// subscribe to the router content
router.Content
|> Observable.subscribe(fun view ->
  // ... do something with the view ...
  ()
)

(**
There's a caveat with the `router.Current` and `router.AdaptiveContent` properties, while both represent the current view, The observable will emit only when there's a view to render.
While the Adaptive Value will emit a `None` value when there's no view to render. So for that reason we recommend using the observable instead, but of course it's up to you to decide which one to use.
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
    fun _ -> async {
      let! token = Async.CancellationToken
      do! Task.Delay(90, token) |> Async.AwaitTask

      return {
        title = "Async"
        content = "This is an async route"
        onAction = None
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
    fun (_, (nav: INavigate<Page>), token) -> task {
      do! Task.Delay(90, token)

      return {
        title = "Task"
        content = "This is a task route"
        onAction =
          Some
          <| fun () -> task {
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
  fun (ctx, _) ->
    let guid = ctx.UrlMatch |> UrlMatch.getFromParams<Guid> "id"

    {
      title = "Param"
      content = $"This is a route with a parameter: {guid}"
      onAction = None
    }
)

(**
## The INavigate<'View> interface

The `cref:T:Navs.INavigate<'View>` is provided so you can perform `Navigate` and `NavigateByname` operations within your view handlers.
Perhaps when a button is clicked, or when a link is clicked, you can use the `INavigate` interface to navigate to a different route.
*)

Route.define<Page>(
  "param",
  "/param/:id<guid>",
  fun (ctx, _) ->
    let guid = ctx.UrlMatch |> UrlMatch.getFromParams<Guid> "id"

    {
      title = "Param"
      content = $"This is a route with a parameter: {guid}"
      onAction = None
    }
)


(**
The UrlMatch property in the context has algo access to the QueryParams and the Hash of the URL that was matched for this route.

## Guards

Guards are a way to prevent a route from being activated or deactivated. They run before any navigation is performed, the `Can Activate` guards run first followed by the `Can Deactivate` guards.
If any of these guards return a `false` value, the navigation will not initiate at all and the `NavigationError<T>` will be returned from the `router.Navigate` call.

Guards also have access to the `RouteContext` object, so you can use the `RouteContext` object to make decisions based on the current route. In a similar fashion, remember that
the longer it takes to resolve the guard, the longer it will take to navigate to the route.
*)

asyncRoute
|> Route.canActivate(fun routeContext -> async {
  let! token = Async.CancellationToken
  do! Task.Delay(90, token) |> Async.AwaitTask
  // return true to allow the navigation
  // return false to prevent the navigation
  return true
})
|> Route.canDeactivate(fun routeContext -> async {
  let! token = Async.CancellationToken
  do! Task.Delay(90, token) |> Async.AwaitTask
  // return true to allow the navigation
  // return false to prevent the navigation
  return true
})

(**
## Caching

The default behavior of the router is to obtain the view from an internal cache if it's available. However, you can change this behavior by using the `Route.cache` function.
Which will make the router always re-execute the route handler when the route is activated.
*)

asyncRoute |> Route.cache CacheStrategy.NoCache
