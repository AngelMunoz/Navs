module Navs.Tests.DSL

open System
open Expecto
open Navs
open System.Threading.Tasks

module Routes =

  let defaultRoute = {
    name = ""
    pattern = ""
    getContent = (fun _ _ _ -> Task.FromResult(""))
    children = []
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
              getContent = (fun _ _ _ -> Task.FromResult("Home"))
        }

        Expect.equal route.name expected.name "Name should be equal"
        Expect.equal route.pattern expected.pattern "Pattern should be equal"

        let! actual =
          route.getContent
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>

        let! expected =
          expected.getContent
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>

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
              getContent = fun _ _ _ -> Task.FromResult("Home")
        }

        Expect.equal route.name expected.name "Name should be equal"
        Expect.equal route.pattern expected.pattern "Pattern should be equal"

        let! actual =
          route.getContent
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>

        let! expected =
          expected.getContent
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>

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
              getContent = fun _ _ _ -> Task.FromResult("Home")
        }

        Expect.equal route.name expected.name "Name should be equal"
        Expect.equal route.pattern expected.pattern "Pattern should be equal"

        let! actual =
          route.getContent
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>

        let! expected =
          expected.getContent
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>
            Unchecked.defaultof<_>

        Expect.equal actual expected "GetContent should be equal"
      }

    ]
