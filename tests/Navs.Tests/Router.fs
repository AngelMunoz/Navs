module Navs.Tests.Router

#nowarn "25" // infomplete pattern match

open Expecto
open FSharp.Data.Adaptive

open Navs
open Navs.Router
open UrlTemplates.RouteMatcher

module Navigation =


  let tests =
    testList "Navs Navigation Tests" [

      test "Router is initialized without any views" {
        let routes = [ Route.define("home", "/", (fun (_, _) -> "Home")) ]
        let router = Router<string>(RouteTracks.fromDefinitions routes)
        let view = router.AdaptiveContent |> AVal.force

        Expect.equal None view "View should be None"
      }

      test "The Splash view should be provided before any navigation" {
        let routes = [ Route.define("home", "/", (fun (_, _) -> "Home")) ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = fun _ -> "Splash"
          )

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal "Splash" view "View should be Splash"
      }

      testTask "The view should be updated when the route changes" {
        let routes = [ Route.define("home", "/", (fun (_, _) -> "Home")) ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = fun _ -> "Splash"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! (Ok _) = router.Navigate("/")

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"
        Expect.equal view "Home" "View should be Home"
      }

      testTask "The view should be the fallback if not found" {
        let routes = [ Route.define("home", "/", (fun (_, _) -> "Home")) ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! (Error(RouteNotFound value)) = router.Navigate("/not-found")

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal value "/not-found" "Value should be /not-found"

        Expect.equal view "Fallback" "View should be Fallback"

      }

      testTask "The view should change any time there's a successful navigation" {

        let routes = [
          Route.define("home", "/", (fun (_, _) -> "Home"))
          Route.define("about", "/about", (fun (_, _) -> "About"))
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! (Ok _) = router.Navigate("/about")
        let (Some firstNavigation) = router.AdaptiveContent |> AVal.force

        let! (Ok _) = router.Navigate("/")
        let (Some secondNavigation) = router.AdaptiveContent |> AVal.force


        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal firstNavigation "About" "First navigation should be About"

        Expect.equal secondNavigation "Home" "Second navigation should be Home"
      }

      testTask "Children can be navigated" {
        let routes = [
          Route.define("users", "/users", (fun (_, _) -> "Users"))
          |> Route.child(
            Route.define(
              "user",
              "/:id<int>",
              (fun (ctx, _) ->
                let (ValueSome userId) =
                  UrlMatch.getFromParams<int> "id" ctx.UrlMatch

                $"User - %i{userId}"
              )
            )
          )
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! (Ok _) = router.Navigate("/users")
        let (Some users) = router.AdaptiveContent |> AVal.force

        let! (Ok _) = router.Navigate("/users/1")
        let (Some view) = router.AdaptiveContent |> AVal.force


        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal users "Users" "Users should be the first navigation"

        Expect.equal view "User - 1" "View should be User - 1"
      }

      testTask "INavigable<_> can navigate from a route" {
        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun (_, nav: INavigate<_>) -> async {
              match! nav.Navigate("/about") |> Async.AwaitTask with
              | Ok _ -> return "About"
              | Error _ -> return "Error"
            }
          )
          Route.define("about", "/about", (fun (_, nav) -> "About"))
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! (Ok _) = router.Navigate("/")

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "About" "View should be About"
      }

    ]

module Guards =

  let tests =
    testList "Guard Tests" [
      testTask "Attempting to activate fails if canActivate returns false" {
        let routes = [
          Route.define("home", "/", (fun (_, _) -> "Home"))
          |> Route.canActivate((fun _ -> async { return false }))
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! Error(CantActivate definition) = router.Navigate("/")

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal definition.Name "home" "Definition name should be home"

      }

      testTask "Attempting to activate succeeds if canActivate returns true" {
        let routes = [
          Route.define("home", "/", (fun (_, _) -> "Home"))
          |> Route.canActivate((fun _ -> async { return true }))
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! (Ok _) = router.Navigate("/")

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Home" "View should be Home"
      }

      testTask
        "Attempting to navigate away fails if canDeactivate returns false" {
        let routes = [
          Route.define("home", "/", (fun (_, _) -> "Home"))
          |> Route.canDeactivate((fun _ -> async { return false }))
          Route.define("about", "/about", (fun (_, _) -> "About"))
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! (Ok _) = router.Navigate("/")

        let! Error(CantDeactivate definition) = router.Navigate("/about")

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Home" "View should be Home"

        Expect.equal definition.Name "home" "Definition name should be home"

      }

      testTask
        "Attempting to navigate away succeeds if canDeactivate returns true" {
        let routes = [
          Route.define("home", "/", (fun (_, _) -> "Home"))
          |> Route.canDeactivate((fun _ -> async { return true }))
          Route.define("about", "/about", (fun (_, _) -> "About"))
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! (Ok _) = router.Navigate("/")

        let! (Ok _) = router.Navigate("/about")

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "About" "View should be About"
      }

      testTask "Parent Guard can prevent child activation" {
        let routes = [
          Route.define("users", "/users", (fun (_, _) -> "Users"))
          |> Route.canActivate((fun _ -> async { return false }))
          |> Route.child(
            Route.define(
              "user",
              "/:id<int>",
              (fun (ctx, _) ->
                let (ValueSome userId) =
                  UrlMatch.getFromParams<int> "id" ctx.UrlMatch

                $"User - %i{userId}"
              )
            )
          )
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! Error(CantActivate definition) = router.Navigate("/users/1")

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Splash" "View should be Splash"

        Expect.equal definition.Name "users" "Definition name should be users"
      }

      testTask "Parent Guard can allow child activation" {
        let routes = [
          Route.define("users", "/users", (fun (_, _) -> "Users"))
          |> Route.canActivate((fun _ -> async { return true }))
          |> Route.child(
            Route.define(
              "user",
              "/:id<int>",
              (fun (ctx, _) ->
                let (ValueSome userId) =
                  UrlMatch.getFromParams<int> "id" ctx.UrlMatch

                $"User - %i{userId}"
              )
            )
          )
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! (Ok _) = router.Navigate("/users/1")

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "User - 1" "View should be User - 1"
      }

      testTask "Child Guard can prevent deactivation" {
        let routes = [
          Route.define("users", "/users", (fun (_, _) -> "Users"))
          |> Route.child(
            Route.define(
              "user",
              "/:id<int>",
              (fun (ctx, _) ->
                let (ValueSome userId) =
                  UrlMatch.getFromParams<int> "id" ctx.UrlMatch

                $"User - %i{userId}"
              )
            )
            |> Route.canActivate(fun _ -> async { return false })
          )
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! Error(CantActivate definition) = router.Navigate("/users/1")

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Splash" "View should be Splash"

        Expect.equal definition.Name "user" "Definition name should be user"
      }
    ]


module Cancellation =
  open System.Threading
  open System.Threading.Tasks

  let tests =
    testList "Navigation cancellation tests" [

      testTask "Navigation can be cancelled" {
        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun (_, _) -> async {
              do! Async.Sleep 5000
              return "Home"
            }
          )
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let cts = new CancellationTokenSource(10)

        let! Error(NavigationCancelled) =
          router.Navigate("/", cancellationToken = cts.Token)


        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Splash" "View should be Splash"

        cts.Dispose()

      }

      testTask "Navigation can be cancelled while checking a guard" {

        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun (_, _) -> async {
              do! Async.Sleep 5000
              return "Home"
            }
          )
          |> Route.canActivate(fun _ -> async {
            do! Async.Sleep 5000
            return true
          })
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let cts = new CancellationTokenSource(10)

        let! Error(NavigationCancelled) =
          router.Navigate("/", cancellationToken = cts.Token)

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Splash" "View should be Splash"

        cts.Dispose()

      }

      testTask "Route Token can cance inner navigations" {
        let mutable count = 0

        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun (_, nav: INavigate<_>) -> async {
              count <- count + 1
              let! token = Async.CancellationToken

              do!
                nav.Navigate("/about", token) |> Async.AwaitTask |> Async.Ignore

              return "Home"
            }
          )
          Route.define<string>(
            "about",
            "/about",
            fun (_, _) -> async {
              do! Async.Sleep 5000
              count <- count + 1
              return "About"
            }
          )
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let cts = new CancellationTokenSource(200)

        let! Error(NavigationCancelled) = router.Navigate("/", cts.Token)

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Splash" "View should be Splash"

        Expect.equal count 1 "Count should be 1"

        cts.Dispose()

      }
      testTask
        "Navigations are successful and inner navigations can be cancelled" {
        let mutable count = 0

        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun (_, nav: INavigate<_>) -> async {
              count <- count + 1
              let! token = Async.CancellationToken

              let navigate () = nav.Navigate("/about", token) |> ignore

              navigate()

              return $"Home 1"
            }
          )
          Route.define<string>(
            "about",
            "/about",
            fun (_, _) -> async {
              count <- count + 1
              do! Async.Sleep 5000
              count <- count + 1
              return "About"
            }
          )
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        let cts = new CancellationTokenSource(200)

        // First navigation count++
        // inner navigation starts count++
        // inner navigation cancels, count remains the same
        let! Ok _ = router.Navigate("/", cts.Token)

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal view "Home 1" "Home 1 should be the view"
        // allow the inner navigation to get cancelled
        // by the cancellation token of the first navigation
        do! Task.Delay 250

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal view "Home 1" "Home 1 should be the view"

        Expect.equal count 2 "Count should be 2"

        cts.Dispose()

      }

      testTask "Navigation completes before cancellation and it doesn't throw" {
        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun (_, _) -> async {
              do! Async.Sleep 10
              return "Home"
            }
          )
          Route.define<string>("about", "/about", (fun (_, _) -> "About"))
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let cts = new CancellationTokenSource(200)

        let! Ok _ = router.Navigate("/", cts.Token)

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"
        Expect.equal view "Home" "Home should be the view"

        cts.Dispose()
      }

      testTask "Navigation can be cancelled while checking deactivate guards" {
        let routes = [
          Route.define<string>("home", "/", (fun (_, _) -> "Home"))
          |> Route.canDeactivate(fun _ -> async {
            do! Async.Sleep 5000
            return true
          })
          Route.define<string>("about", "/about", (fun (_, _) -> "About"))
        ]

        let router =
          Router<string>(
            RouteTracks.fromDefinitions routes,
            splash = (fun _ -> "Splash"),
            notFound = fun _ -> "Fallback"
          )

        let (Some splash) = router.AdaptiveContent |> AVal.force

        let! Ok _ = router.Navigate("/")

        let cts = new CancellationTokenSource(100)

        let! Error(NavigationCancelled) = router.Navigate("/about", cts.Token)

        let (Some view) = router.AdaptiveContent |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Home" "View should be Home"

        cts.Dispose()

      }
    ]


[<Tests>]
let tests =
  testList "Navs Router Tests" [
    Navigation.tests
    Guards.tests
    Cancellation.tests
  ]
