module Navs.Tests.DSL

open System
open Expecto
open Navs
open System.Threading.Tasks

module Routes =

  let defaultRoute = {
    Name = ""
    Pattern = ""
    GetContent = Func<_, _, _, _>(fun ctx nav _ -> Task.FromResult(""))
    Children = []
    CanActivate = []
    CanDeactivate = []
    CacheStrategy = Cache
  }

  [<Tests>]
  let tests =
    testList "Navs Route Tests" [
      testTask "define<View> should the correct route definition<view>" {
        let route = Route.define("home", "/", (fun (_, _) -> "Home"))

        let expected = {
          defaultRoute with
              Name = "home"
              Pattern = "/"
              GetContent =
                Func<_, _, _, _>(fun ctx nav _ -> Task.FromResult("Home"))
        }

        Expect.equal route.Name expected.Name "Name should be equal"
        Expect.equal route.Pattern expected.Pattern "Pattern should be equal"

        let! actual =
          route.GetContent.Invoke(
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>
          )

        let! expected =
          expected.GetContent.Invoke(
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>
          )

        Expect.equal actual expected "GetContent should be equal"
      }

      testTask
        "define<view> should create the correct route definition<view> Async" {
        let route =
          Route.define<string>(
            "home",
            "/",
            (fun (_, _) -> async { return "Home" })
          )

        let expected = {
          defaultRoute with
              Name = "home"
              Pattern = "/"
              GetContent =
                Func<_, _, _, _>(fun ctx nav token -> Task.FromResult("Home"))
        }

        Expect.equal route.Name expected.Name "Name should be equal"
        Expect.equal route.Pattern expected.Pattern "Pattern should be equal"

        let! actual =
          route.GetContent.Invoke(
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>
          )

        let! expected =
          expected.GetContent.Invoke(
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>
          )

        Expect.equal actual expected "GetContent should be equal"

      }

      testTask
        "define<view> should create the correct route definition<view> Task" {
        let route =
          Route.define<string>(
            "home",
            "/",
            (fun (_, _, _) -> task { return "Home" })
          )

        let expected = {
          defaultRoute with
              Name = "home"
              Pattern = "/"
              GetContent =
                Func<_, _, _, _>(fun ctx nav token -> Task.FromResult("Home"))
        }

        Expect.equal route.Name expected.Name "Name should be equal"
        Expect.equal route.Pattern expected.Pattern "Pattern should be equal"

        let! actual =
          route.GetContent.Invoke(
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>
          )

        let! expected =
          expected.GetContent.Invoke(
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>
          )

        Expect.equal actual expected "GetContent should be equal"
      }

    ]


[<Tests>]
let tests = testList "Navs DSL Tests" [ Routes.tests ]
