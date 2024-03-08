module UrlTemplates.Tests.RouteMatcher

open Expecto
open UrlTemplates.RouteMatcher

module MatchTemplate =

  [<Tests>]
  let tests = testList "MatchTemplate Tests" []


module MatchUrl =

  [<Tests>]
  let tests =
    testList "MatchUrl Tests" [

    ]


module MatchStrings =
  open UrlTemplates.UrlTemplate

  let tests =
    testList "MatchStrings Tests" [
      testCase "It can parse a segment only"
      <| fun _ ->
        let actualTemplate = "/hello/world"
        let actualUrl = "/hello/world"
        let actual = RouteMatcher.matchStrings actualTemplate actualUrl

        match actual with
        | Ok(urlTpl, urlMatch, urlInfo) ->
          Expect.equal
            urlTpl.Segments
            [ Plain ""; Plain "hello"; Plain "world" ]
            "Segments should match"

          Expect.isEmpty urlTpl.Query "Query should be empty"

          Expect.equal urlTpl.Hash ValueNone "Hash should be empty"

          Expect.isEmpty urlMatch.Query "Query should be empty"

          Expect.equal urlMatch.Hash ValueNone "Hash should be empty"

          Expect.equal
            urlMatch.Segments
            [ ""; "hello"; "world" ]
            "Segments should match"

          Expect.isEmpty urlInfo.Params "Params should be empty"

          Expect.isEmpty urlInfo.QueryParams "QueryParams should be empty"

          Expect.equal urlInfo.Hash ValueNone "Hash should be empty"

        | Error e -> failtestf "Expected Ok, got Error %A" e
    ]


[<Tests>]
let tests =
  testList "RouteMatcher Tests" [
    MatchTemplate.tests
    MatchUrl.tests
    MatchStrings.tests
  ]
