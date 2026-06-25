module Navs.Tests.DSL

open System
open Expecto
open Navs
open System.Threading.Tasks

module Routes =
  open System.Threading

  let defaultRoute = {
    name = ""
    pattern = ""
    getContent = GetView<_>(fun _ _ -> fun token -> ValueTask.FromResult(""))
    canActivate = []
    canDeactivate = []
    cacheStrategy = Cache
  }

  [<Tests>]
  let tests =
    testList "Navs Route DSL Tests" [
      testCaseTask "define<View> should the correct route definition<view> sync"
      <| fun () -> task {
        let route = Route.define("home", "/", (fun _ _ -> "Home"))

        let expected = {
          defaultRoute with
              name = "home"
              pattern = "/"
              getContent =
                GetView<_>(fun _ _ -> fun token -> ValueTask.FromResult("Home"))
        }

        Expect.equal route.name expected.name "Name should be equal"
        Expect.equal route.pattern expected.pattern "Pattern should be equal"
        let token = CancellationToken.None

        let! actual =
          route.getContent.Invoke
            (Unchecked.defaultof<_>, Unchecked.defaultof<_>)
            token

        let! expected =
          expected.getContent.Invoke
            (Unchecked.defaultof<_>, Unchecked.defaultof<_>)
            token

        Expect.equal actual expected "GetContent should be equal"
      }

      testCaseTask
        "define<view> should create the correct route definition<view> Async"
      <| fun () -> task {
        let route =
          Route.define<string>(
            "home",
            "/",
            (fun _ _ -> async { return "Home" })
          )

        let expected = {
          defaultRoute with
              name = "home"
              pattern = "/"
              getContent =
                GetView<_>(fun _ _ -> fun token -> ValueTask.FromResult("Home"))
        }

        Expect.equal route.name expected.name "Name should be equal"
        Expect.equal route.pattern expected.pattern "Pattern should be equal"
        let token = CancellationToken.None

        let! actual =
          route.getContent.Invoke
            (Unchecked.defaultof<_>, Unchecked.defaultof<_>)
            token

        let! expected =
          expected.getContent.Invoke
            (Unchecked.defaultof<_>, Unchecked.defaultof<_>)
            token

        Expect.equal actual expected "GetContent should be equal"

      }

      testCaseTask
        "define<view> should create the correct route definition<view> Task"
      <| fun () -> task {
        let route =
          Route.define<string>(
            "home",
            "/",
            (fun _ _ _ -> task { return "Home" })
          )

        let expected = {
          defaultRoute with
              name = "home"
              pattern = "/"
              getContent =
                GetView<_>(fun _ _ -> fun token -> ValueTask.FromResult("Home"))
        }

        Expect.equal route.name expected.name "Name should be equal"
        Expect.equal route.pattern expected.pattern "Pattern should be equal"
        let token = CancellationToken.None

        let! actual =
          route.getContent.Invoke
            (Unchecked.defaultof<_>, Unchecked.defaultof<_>)
            token

        let! expected =
          expected.getContent.Invoke
            (Unchecked.defaultof<_>, Unchecked.defaultof<_>)
            token

        Expect.equal actual expected "GetContent should be equal"
      }

    ]
