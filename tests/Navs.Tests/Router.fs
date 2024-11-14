module Navs.Tests.Router

#nowarn "25" // incomplete pattern match

open System
open System.Threading

open Expecto
open FSharp.Data.Adaptive

open FsToolkit.ErrorHandling

open Navs
open Navs.Router
open UrlTemplates.RouteMatcher

module NavigationState =

  let tests () =
    testList "NavigationState tests" [
      test "State should be Idle by default" {
        let router = Router.build<string>([], (fun _ -> "Splash"))

        let state = router.State |> AVal.force

        Expect.equal state Idle "State should be Idle"
      }

      testCaseTask "State should be Navigating when navigating"
      <| fun () -> task {
        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun _ _ -> async {
              do! Async.Sleep(TimeSpan.FromSeconds(10))
              return "Home"
            }
          )
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let state = router.State |> AVal.force

        Expect.equal state Idle "State should be Idle"
        let cts = new CancellationTokenSource()
        let mutable state = Unchecked.defaultof<NavigationState>

        router.Navigate("/", cts.Token)
        |> Async.AwaitTask
        |> AsyncResult.teeError(fun v ->
          if v.IsNavigationCancelled then
            state <- router.State |> AVal.force
        )
        |> Async.Ignore
        |> Async.StartImmediate

        let state = router.State |> AVal.force

        Expect.equal state Navigating "State should be Navigating"

        cts.Cancel() // cancel navigation

        let state = router.State |> AVal.force

        Expect.equal state Idle "State should be Idle"
      }
    ]

module RouteContext =


  let tests () =
    testList "RouteContext tests" [
      testCaseTask "RouteContext should contain the path"
      <| fun () -> task {
        let router =
          Router.build<string>(
            [
              Route.define("home", "/", (fun _ _ -> "Home"))
              Route.define("about", "/about", (fun _ _ -> "About"))
            ],
            (fun _ -> "Splash")
          )

        let context = router.Route |> AVal.force

        match context with
        | ValueNone -> ()
        | _ -> failtest "There should not be a route initially"

        let! (Ok _) = router.Navigate("/")

        let (ValueSome context) = router.Route |> AVal.force

        Expect.equal context.path "/" "Path should be /"

        let! (Ok _) = router.Navigate("/about")

        let (ValueSome context) = router.Route |> AVal.force

        Expect.equal context.path "/about" "Path should be /about"
      }

      testTask
        "Context should equal to the last context if the next was cancelled" {
        let router =
          Router.build(
            [
              Route.define<string>(
                "home",
                "/?complete<bool>",
                fun ctx _ -> async {
                  match
                    UrlMatch.getFromParams<bool> "complete" ctx.urlMatch
                  with
                  | ValueSome true -> return "Home"
                  | _ ->
                    do! Async.Sleep(TimeSpan.FromSeconds(10))
                    return "Home"
                }
              )
            ]
          )


        let context = router.Route |> AVal.force

        match context with
        | ValueNone -> ()
        | _ -> failtest "There should not be a route initially"


        let! (Ok _) = router.Navigate("/?complete=true")

        let (ValueSome context) = router.Route |> AVal.force

        Expect.equal
          context.path
          "/?complete=true"
          "Path should be /?complete=true"

        let cts = new CancellationTokenSource(100)
        let! _ = router.Navigate("/?complete=false", cts.Token)

        let context =
          router.Route
          |> AVal.force
          |> ValueOption.defaultWith(fun _ ->
            failtest "Context should not be None"
          )

        let value =
          context.urlMatch
          |> UrlMatch.getFromParams<bool> "complete"
          |> ValueOption.defaultWith(fun _ ->
            failtest "Complete should be in the params"
          )

        Expect.isTrue value "Complete should be true"

        cts.Dispose()
      }

    ]

module Navigation =
  type StatefulRoute = {
    id: int
    state: ref<int>
    updateState: unit -> unit
  }

  let tests () =
    testList "Navs Navigation Tests" [

      test "Router is initialized without any views" {
        let routes = [ Route.define("home", "/", (fun _ _ -> "Home")) ]
        let router = Router.build<string>(routes)
        let view = router.Content |> AVal.force

        Expect.equal ValueNone view "View should be None"
      }

      test "The Splash view should be provided before any navigation" {
        let routes = [ Route.define("home", "/", (fun _ _ -> "Home")) ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal "Splash" view "View should be Splash"
      }

      testCaseTask "The view should be updated when the route changes"
      <| fun () -> task {
        let routes = [ Route.define("home", "/", (fun _ _ -> "Home")) ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let! (Ok _) = router.Navigate("/")

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"
        Expect.equal view "Home" "View should be Home"
      }

      testCaseTask "The view should be the fallback if not found"
      <| fun () -> task {
        let routes = [ Route.define("home", "/", (fun _ _ -> "Home")) ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let! (Error(RouteNotFound value)) = router.Navigate("/not-found")

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal value "/not-found" "Value should be /not-found"

        Expect.equal view "Splash" "View should be Splash"

      }

      testCaseTask
        "The view should change any time there's a successful navigation"
      <| fun () -> task {

        let routes = [
          Route.define("home", "/", (fun _ _ -> "Home"))
          Route.define("about", "/about", (fun _ _ -> "About"))
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let! (Ok _) = router.Navigate("/about")
        let (ValueSome firstNavigation) = router.Content |> AVal.force

        let! (Ok _) = router.Navigate("/")
        let (ValueSome secondNavigation) = router.Content |> AVal.force


        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal firstNavigation "About" "First navigation should be About"

        Expect.equal secondNavigation "Home" "Second navigation should be Home"
      }

      testCaseTask "State is preserved if the route is the same"
      <| fun () -> task {

        let routes = [
          Route.define<StatefulRoute>(
            "user",
            "/?id<int>",
            fun ctx _ -> async {
              let (ValueSome userId) =
                UrlMatch.getFromParams<int> "id" ctx.urlMatch

              let state = ref 10

              return {
                id = userId
                state = state
                updateState = (fun () -> state.Value <- state.Value + 1)
              }
            }
          )
        ]

        let router = Router.build<StatefulRoute>(routes)

        let! (Ok _) = router.Navigate("/?id=1")

        let (ValueSome {
                         id = id
                         state = state
                         updateState = updateState
                       }) =
          router.Content |> AVal.force

        Expect.equal id 1 "Id should be 1"
        Expect.equal state.Value 10 "State should be 10"

        updateState()

        let! (Ok _) = router.Navigate("/?id=1")

        let (ValueSome { id = id; state = state }) =
          router.Content |> AVal.force

        Expect.equal id 1 "Id should be 1"
        Expect.equal state.Value 11 "State should be 11"
      }

      testCaseTask "State is not preserved if the query changes"
      <| fun () -> task {

        let routes = [
          Route.define<StatefulRoute>(
            "user",
            "/?id<int>",
            fun ctx _ -> async {
              let (ValueSome userId) =
                UrlMatch.getFromParams<int> "id" ctx.urlMatch

              let state = ref 10

              return {
                id = userId
                state = state
                updateState = (fun () -> state.Value <- state.Value + 1)
              }
            }
          )
        ]

        let router = Router.build<StatefulRoute>(routes)

        let! (Ok _) = router.Navigate("/?id=1")

        let (ValueSome {
                         id = id
                         state = state
                         updateState = updateState
                       }) =
          router.Content |> AVal.force

        Expect.equal id 1 "Id should be 1"

        updateState()

        Expect.equal state.Value 11 "State should be 11"

        let! (Ok _) = router.Navigate("/?id=2")

        let (ValueSome { id = id; state = state }) =
          router.Content |> AVal.force

        Expect.equal id 2 "Id should be 2"
        Expect.equal state.Value 10 "State should be 10"
      }

    ]

module Guards =

  let tests () =
    testList "Guard Tests" [
      testCaseTask
        $"Attempting to activate fails if canActivate returns {nameof Stop}"
      <| fun () -> task {
        let routes = [
          Route.define("home", "/", (fun _ _ -> "Home"))
          |> Route.canActivateAsync(fun _ _ -> async { return Stop })
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let! Error(CantActivate definition) = router.Navigate("/")

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal definition "/" "Definition name should be home"

      }

      testCaseTask
        $"Attempting to activate succeeds if canActivate returns {nameof Continue}"
      <| fun () -> task {
        let routes = [
          Route.define("home", "/", (fun _ _ -> "Home"))
          |> Route.canActivateAsync(fun _ _ -> async { return Continue })
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let! (Ok _) = router.Navigate("/")

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Home" "View should be Home"
      }

      testCaseTask
        $"Attempting to navigate away fails if canDeactivate returns {nameof Stop}"
      <| fun () -> task {
        let routes = [
          Route.define("home", "/", (fun _ _ -> "Home"))
          |> Route.canDeactivateAsync(fun _ _ -> async { return Stop })
          Route.define("about", "/about", (fun _ _ -> "About"))
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let! (Ok _) = router.Navigate("/")

        let! Error(CantDeactivate definition) = router.Navigate("/about")

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Home" "View should be Home"

        Expect.equal definition "/" "Definition name should be home"
      }

      testCaseTask
        $"Attempting to navigate away succeeds if canDeactivate returns {nameof Continue}"
      <| fun () -> task {
        let routes = [
          Route.define("home", "/", (fun _ _ -> "Home"))
          |> Route.canDeactivateAsync(fun _ _ -> async { return Continue })
          Route.define("about", "/about", (fun _ _ -> "About"))
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let! (Ok _) = router.Navigate("/")

        let! (Ok _) = router.Navigate("/about")

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "About" "View should be About"
      }
    ]


module Cancellation =

  let tests () =
    testList "Navigation cancellation tests" [

      testCaseTask "Navigation can be cancelled"
      <| fun () -> task {
        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun _ _ -> async {
              do! Async.Sleep 5000
              return "Home"
            }
          )
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let cts = new CancellationTokenSource(10)

        let! Error(NavigationCancelled) =
          router.Navigate("/", cancellationToken = cts.Token)


        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Splash" "View should be Splash"

        cts.Dispose()

      }

      testCaseTask "Navigation can be cancelled while checking a guard"
      <| fun () -> task {

        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun _ _ -> async {
              do! Async.Sleep 5000
              return "Home"
            }
          )
          |> Route.canActivateAsync(fun _ _ -> async {
            do! Async.Sleep 5000
            return Redirect "/login"
          })
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let cts = new CancellationTokenSource(10)

        let! Error(NavigationCancelled) =
          router.Navigate("/", cancellationToken = cts.Token)

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Splash" "View should be Splash"

        cts.Dispose()

      }

      testCaseTask "Route Token can cancel inner navigations"
      <| fun () -> task {
        let mutable count = 0

        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun _ (nav: INavigable<_>) -> async {
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
            fun _ _ -> async {
              do! Async.Sleep 5000
              count <- count + 1
              return "About"
            }
          )
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let cts = new CancellationTokenSource(200)

        let! Error(NavigationCancelled) = router.Navigate("/", cts.Token)

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Splash" "View should be Splash"

        Expect.equal count 1 "Count should be 1"

        cts.Dispose()

      }
      testCaseTask
        "Navigations are successful and inner navigations can be cancelled"
      <| fun () -> task {
        let mutable count = 0

        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun _ (nav: INavigable<_>) -> async {
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
            fun _ _ -> async {
              count <- count + 1
              do! Async.Sleep 5000
              count <- count + 1
              return "About"
            }
          )
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        let cts = new CancellationTokenSource()

        // First navigation count++
        // inner navigation starts count++
        // inner navigation cancels, count remains the same
        match! router.Navigate("/", cts.Token) with
        | Ok _ -> ()
        | Error e -> failtestf $"Navigation should not fail %A{e}"

        match router.Content |> AVal.force with
        | ValueSome view ->
          Expect.equal view "Home 1" "Home 1 should be the view"
        | _ -> failtestf "View should not be None"

        // allow the inner navigation to get cancelled
        // by the cancellation token of the first navigation
        cts.Cancel()

        match router.Content |> AVal.force with
        | ValueSome view ->
          Expect.equal view "Home 1" "Home 1 should be the view"
        | _ -> failtestf "View should not be None"

        Expect.equal count 2 "Count should be 2"

        cts.Dispose()

      }

      testCaseTask
        "Navigation completes before cancellation and it doesn't throw"
      <| fun () -> task {
        let routes = [
          Route.define<string>(
            "home",
            "/",
            fun _ _ -> async {
              do! Async.Sleep 10
              return "Home"
            }
          )
          Route.define<string>("about", "/about", (fun _ _ -> "About"))
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let cts = new CancellationTokenSource()
        cts.CancelAfter(1000)
        let! Ok _ = router.Navigate("/", cts.Token)

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"
        Expect.equal view "Home" "Home should be the view"

        cts.Dispose()
      }

      testCaseTask
        "Navigation can be cancelled while checking deactivate guards"
      <| fun () -> task {
        let routes = [
          Route.define<string>("home", "/", (fun _ _ -> "Home"))
          |> Route.canDeactivateAsync(fun _ _ -> async {
            do! Async.Sleep 5000
            return Continue
          })
          Route.define<string>("about", "/about", (fun _ _ -> "About"))
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let (ValueSome splash) = router.Content |> AVal.force

        let! Ok _ = router.Navigate("/")

        let cts = new CancellationTokenSource(100)

        let! Error(NavigationCancelled) = router.Navigate("/about", cts.Token)

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal splash "Splash" "Splash should be the initial view"

        Expect.equal view "Home" "View should be Home"

        cts.Dispose()

      }
    ]


module Redirect =
  let tests () =
    testList "Redirection Tests" [
      testCaseTask "Navigation can be redirected to another route"
      <| fun () -> task {
        let routes = [
          Route.define<string>("home", "/", fun _ _ -> "Home")
          |> Route.canActivate(fun _ _ -> Redirect "/about")
          Route.define<string>("about", "/about", (fun _ _ -> "About"))
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let! Ok _ = router.Navigate("/")

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal view "About" "View should be About"
      }

      testCaseTask "Navigation can be redirected to another when deactivating"
      <| fun () -> task {
        let routes = [
          Route.define<string>("home", "/", fun _ _ -> "Home")
          |> Route.canDeactivate(fun _ _ -> Redirect "/about")
          Route.define<string>("about", "/about", (fun _ _ -> "About"))
          Route.define("other", "/other", (fun _ _ -> "Other"))
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let! Ok _ = router.Navigate("/")

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal view "Home" "View should be Home"

        let! Ok _ = router.Navigate("/other")

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal view "About" "View should be About"
      }

      testCaseTask "Redirection Follows at least 3 navigations"
      <| fun () -> task {
        let routes = [
          Route.define<string>("home", "/", fun _ _ -> "Home")
          |> Route.canActivate(fun _ _ -> Redirect "/about")
          Route.define<string>("about", "/about", (fun _ _ -> "About"))
          |> Route.canActivate(fun _ _ -> Redirect "/other")
          Route.define("other", "/other", (fun _ _ -> "Other"))
          |> Route.canActivate(fun _ _ -> Redirect "/other2")
          Route.define("other2", "/other2", (fun _ _ -> "Other2"))
        ]

        let router = Router.build<string>(routes, (fun _ -> "Splash"))

        let! Ok _ = router.Navigate("/")

        let (ValueSome view) = router.Content |> AVal.force

        Expect.equal view "Other2" "View should be Other2"

      }
    ]


[<Tests>]
let tests =
  testList "Navs Router Tests" [
    NavigationState.tests()
    RouteContext.tests()
    Navigation.tests()
    Guards.tests()
    Cancellation.tests()
    Redirect.tests()
  ]
