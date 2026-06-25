module UrlTemplates.Tests.RouteMatcher

#nowarn "25"

open Expecto
open UrlTemplates
open UrlTemplates.RouteMatcher

module Params =
  open UrlTemplates.UrlTemplate

  [<Tests>]
  let tests =
    testList "URL Params Tests" [

      test "UrlTemplate parses the correct param segments" {

        let actualTemplate = "/api/v1/users/:userId/posts/:postId<int>"
        let actualUrl = "/api/v1/users/123/posts/456"

        let urlTemplate, _, _ =
          RouteMatcher.matchStrings actualTemplate actualUrl
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        Expect.equal
          urlTemplate.Segments
          [
            Plain ""
            Plain "api"
            Plain "v1"
            Plain "users"
            ParamSegment("userId", String)
            Plain "posts"
            ParamSegment("postId", Int)
          ]
          "Segments should match"
      }

      test "UrlMatch does not allow '-' in param segments" {
        let actualTemplate = "/api/:user-name"
        let actualUrl = "/api/john+doe"

        match RouteMatcher.matchStrings actualTemplate actualUrl with
        | Ok _ -> failtestf "Expected Error, got Ok"
        | Error [ TemplateParsingError e ] ->

          Expect.stringContains
            e
            "Expecting: Ascii letter"
            "Error message should contain the expected value"
        | Error e -> failtestf "Unexpected Error, got %A" e
      }
    ]

module QueryParams =
  open UrlTemplates.UrlTemplate


  [<Tests>]
  let tests =
    testList "Query Params Tests" [
      test "Optional Query Params are parsed correctly" {
        let actualTemplate = "/api?name&age&status"
        let actualUrl = "/api?name=john&age=30&status=active"

        let urlTemplate, urlMatch, _ =
          RouteMatcher.matchStrings actualTemplate actualUrl
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        Expect.equal
          urlTemplate.Query
          [
            Optional("name", String)
            Optional("age", String)
            Optional("status", String)
          ]
          "Query should match"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlMatch.Query["name"]

        Expect.equal
          value
          "john"
          "name=john should match from the supplied template"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlMatch.Query["age"]

        Expect.equal value "30" "age=30 should match from the supplied template"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlMatch.Query["status"]

        Expect.equal
          value
          "active"
          "status=active should match from the supplied template"

      }

      test "Optional Query Params are not required in the url" {
        let actualTemplate = "/api?name&age&status"
        let actualUrl = "/api?name=john&age=30"

        let urlTemplate, urlInfo, urlMatch =
          RouteMatcher.matchStrings actualTemplate actualUrl
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        Expect.equal
          urlTemplate.Query
          [
            Optional("name", String)
            Optional("age", String)
            Optional("status", String)
          ]
          "Query should match"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["name"]

        Expect.equal value "john" "name=john should match"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["age"]

        Expect.equal value "30" "age=30 should match"

        Expect.throwsT<System.Collections.Generic.KeyNotFoundException>
          (fun () -> urlInfo.Query["status"] |> ignore)
          "Optional QueryValue should not be present in the URL Information"

        let name = urlMatch.QueryParams["name"]

        Expect.equal
          name
          "john"
          "The matched URL should contain the supplied key and value"

        let age = urlMatch.QueryParams["age"]

        Expect.equal
          age
          "30"
          "The matched URL should contain the supplied key and value"

        Expect.throwsT<System.Collections.Generic.KeyNotFoundException>
          (fun () -> urlMatch.QueryParams["status"] |> ignore)
          "Optional QueryValue should not be present in the matched URL"
      }

      test "Required Query Params are parsed correctly" {

        let actualTemplate = "/api?name!&age!&status!"
        let actualUrl = "/api?name=john&age=30&status=active"

        let urlTemplate, urlInfo, _ =
          RouteMatcher.matchStrings actualTemplate actualUrl
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        Expect.equal
          urlTemplate.Query
          [
            Required("name", String)
            Required("age", String)
            Required("status", String)
          ]
          "Query should match"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["name"]

        Expect.equal
          value
          "john"
          "name=john should match from the supplied template"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["age"]

        Expect.equal value "30" "age=30 should match from the supplied template"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["status"]

        Expect.equal
          value
          "active"
          "status=active should match from the supplied template"

      }
      test "Required Query Params are required in the url" {
        let actualTemplate = "/api?name!&age!&status!"
        let actualUrl = "/api?name=john&age=30"

        match RouteMatcher.matchStrings actualTemplate actualUrl with
        | Ok _ -> failtestf "Expected Error, got Ok"
        | Error [ MatchingError(MissingQueryParams [ value ]) ] ->
          Expect.equal value "status" "status should be missing"
        | Error e -> failtestf "Unexpected Error, got %A" e
      }

      test "QueryParams can include '-' or '_' in their name" {

        let actualTemplate = "/api?first-name&last_name"
        let actualUrl = "/api?first-name=john&last_name=doe"

        let urlTemplate, urlInfo, _ =
          RouteMatcher.matchStrings actualTemplate actualUrl
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        Expect.equal
          urlTemplate.Query
          [ Optional("first-name", String); Optional("last_name", String) ]
          "Query should match"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["first-name"]

        Expect.equal
          value
          "john"
          "first-name=john should match from the supplied template"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["last_name"]

        Expect.equal
          value
          "doe"
          "last_name=doe should match from the supplied template"
      }

      test "Query Params can be typed" {
        let actualTemplate = "/api?name&age<int>&id<guid>!"
        let guid = System.Guid.NewGuid()
        let actualUrl = $"/api?age=30&id={guid}&name=john"

        let urlTemplate, urlInfo, urlMatch =
          RouteMatcher.matchStrings actualTemplate actualUrl
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        Expect.equal
          urlTemplate.Query
          [
            Optional("name", String)
            Optional("age", Int)
            Required("id", Guid)
          ]
          "Query should match"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["name"]

        Expect.equal
          value
          "john"
          "name=john should match from the supplied template"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["age"]

        Expect.equal value "30" "age=30 should match from the supplied template"

        let (UrlParser.QueryValue.String(ValueSome value)) = urlInfo.Query["id"]

        Expect.equal
          value
          (guid.ToString())
          "id should match from the supplied template"

        let age = urlMatch.QueryParams["age"]

        Expect.equal
          (unbox<int> age)
          30
          "The matched URL should contain the supplied key and value"

        let id = urlMatch.QueryParams["id"]

        Expect.equal
          (unbox<System.Guid> id)
          guid
          "The matched URL should contain the supplied key and value"

        let name = urlMatch.QueryParams["name"]

        Expect.equal
          (unbox<string> name)
          "john"
          "The matched URL should contain the supplied key and value"
      }
    ]

module MatchStrings =
  open UrlTemplates.UrlTemplate

  [<Tests>]
  let tests =
    testList "MatchStrings Tests" [
      test "UrlTemplate segments, query, and hash contain the expected values" {
        let actualTemplate = "/hello/world"
        let actualUrl = "/hello/world"

        let urlTemplate, _, _ =
          RouteMatcher.matchStrings actualTemplate actualUrl
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        Expect.equal
          urlTemplate.Segments
          [ Plain ""; Plain "hello"; Plain "world" ]
          "Segments should match"

        Expect.isEmpty urlTemplate.Query "Query should be empty"

        Expect.equal urlTemplate.Hash ValueNone "Hash should be empty"
      }

      test " UrlMatch segments, query, and hash contain the expected values" {
        let actualTemplate = "/hello/world"
        let actualUrl = "/hello/world"

        let _, urlInfo, _ =
          RouteMatcher.matchStrings actualTemplate actualUrl
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        Expect.isEmpty urlInfo.Query "Query should be empty"

        Expect.equal urlInfo.Hash ValueNone "Hash should be empty"

        Expect.equal
          urlInfo.Segments
          [ ""; "hello"; "world" ]
          "Segments should match"
      }

      test "UrlInfo segments, query, and hash contain the expected values" {
        let actualTemplate = "/hello/world"
        let actualUrl = "/hello/world"

        let _, _, urlMatch =
          RouteMatcher.matchStrings actualTemplate actualUrl
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        Expect.isEmpty urlMatch.Params "Params should be empty"

        Expect.isEmpty urlMatch.QueryParams "QueryParams should be empty"

        Expect.equal urlMatch.Hash ValueNone "Hash should be empty"
      }

      test "Strings with different segments should not match" {
        let actualTemplate = "/hello/world"
        let actualUrl = "/hello/earth"

        match RouteMatcher.matchStrings actualTemplate actualUrl with
        | Ok _ -> failtestf "Expected Error, got Ok"
        | Error [ MatchingError(SegmentMismatch(name, otherName)) ] ->
          Expect.equal name "world" "world should be missing"
          Expect.equal otherName "earth" "earth should be missing"
        | Error e -> failtestf "Unexpected Error, got %A" e
      }

      test "strings with segments and queries should match" {
        let actualTemplate = "/hello/:world<Guid>?name&age<int>"
        let guid = System.Guid.NewGuid()
        let actualUrl = $"/hello/{guid}?name=john&age=30"

        let urlTemplate, urlInfo, urlMatch =
          RouteMatcher.matchStrings actualTemplate actualUrl
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        Expect.equal
          urlTemplate.Segments
          [ Plain ""; Plain "hello"; ParamSegment("world", Guid) ]
          "Segments should match"

        Expect.equal

          urlTemplate.Query
          [ Optional("name", String); Optional("age", Int) ]
          "Query should match"

        Expect.equal urlTemplate.Hash ValueNone "Hash should be empty"

        Expect.equal
          urlInfo.Segments
          [ ""; "hello"; guid.ToString() ]
          "Segments should match"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["name"]

        Expect.equal value "john" "name=john should match"

        let (UrlParser.QueryValue.String(ValueSome value)) =
          urlInfo.Query["age"]

        Expect.equal value "30" "age=30 should match"

        Expect.equal urlInfo.Hash ValueNone "Hash should be empty"

        let world = urlMatch.Params["world"]

        Expect.equal
          (unbox<System.Guid> world)
          guid
          "The matched URL should contain the supplied key and value"

        let name = urlMatch.QueryParams["name"]

        Expect.equal
          (unbox<string> name)
          "john"
          "The matched URL should contain the supplied key and value"

        let age = urlMatch.QueryParams["age"]

        Expect.equal
          (unbox<int> age)
          30
          "The matched URL should contain the supplied key and value"

        Expect.equal urlMatch.Hash ValueNone "Hash should be empty"
      }
    ]

module UrlMatchExtensions =

  [<Tests>]
  let tests =
    testList "UrlMatch Extensions" [

      test "UrlMatch.getParamFromQuery returns the correct value" {
        let template = "/api?name&age<int>&status"
        let url = "/api?name=john&age=30"

        let _, _, urlMatch =
          RouteMatcher.matchStrings template url
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        let name = UrlMatch.getParamFromQuery<string> "name" urlMatch
        let age = UrlMatch.getParamFromQuery<int> "age" urlMatch
        let status = UrlMatch.getParamFromQuery<string> "status" urlMatch

        Expect.equal name (ValueSome "john") "name should match"
        Expect.equal age (ValueSome 30) "age should match"
        Expect.equal status ValueNone "status should not match"
      }

      test "UrlMatch.getParamFromPath returns the correct value" {
        let template = "/api/:name/:age<int>/:status"
        let url = "/api/john/30/active"

        let _, _, urlMatch =
          RouteMatcher.matchStrings template url
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        let name = UrlMatch.getParamFromPath<string> "name" urlMatch
        let age = UrlMatch.getParamFromPath<int> "age" urlMatch
        let status = UrlMatch.getParamFromPath<string> "status" urlMatch

        Expect.equal name (ValueSome "john") "name should match"
        Expect.equal age (ValueSome 30) "age should match"
        Expect.equal status (ValueSome "active") "status should match"
      }

      test "UrlMatch.getFromParams returns the correct value" {
        let template = "/api?name&age<int>&status"
        let url = "/api?name=john&age=30"

        let _, _, urlMatch =
          RouteMatcher.matchStrings template url
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        let name = UrlMatch.getFromParams<string> "name" urlMatch
        let age = UrlMatch.getFromParams<int> "age" urlMatch
        let status = UrlMatch.getFromParams<string> "status" urlMatch

        Expect.equal name (ValueSome "john") "name should match"
        Expect.equal age (ValueSome 30) "age should match"
        Expect.equal status ValueNone "status should not match"
      }

      test "UrlMatchExtensions.getParamFromQuery returns the correct value" {
        let template = "/api?name&age<int>&status"
        let url = "/api?name=john&age=30"

        let _, _, urlMatch =
          RouteMatcher.matchStrings template url
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        let name = urlMatch.getParamFromQuery<string>("name")
        let age = urlMatch.getParamFromQuery<int>("age")
        let status = urlMatch.getParamFromQuery<string>("status")

        Expect.equal name (ValueSome "john") "name should match"
        Expect.equal age (ValueSome 30) "age should match"
        Expect.equal status ValueNone "status should not match"
      }

      test "UrlMatchExtensions.getParamFromPath returns the correct value" {
        let template = "/api/:name/:age<int>/:status"
        let url = "/api/john/30/active"

        let _, _, urlMatch =
          RouteMatcher.matchStrings template url
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        let name = urlMatch.getParamFromPath<string>("name")
        let age = urlMatch.getParamFromPath<int>("age")
        let status = urlMatch.getParamFromPath<string>("status")

        Expect.equal name (ValueSome "john") "name should match"
        Expect.equal age (ValueSome 30) "age should match"
        Expect.equal status (ValueSome "active") "status should not match"
      }

      test "UrlMatchExtensions.getFromParams returns the correct value" {
        let template = "/api?name&age<int>&status"
        let url = "/api?name=john&age=30"

        let _, _, urlMatch =
          RouteMatcher.matchStrings template url
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        let name = urlMatch.getFromParams<string>("name")
        let age = urlMatch.getFromParams<int>("age")
        let status = urlMatch.getFromParams<string>("status")

        Expect.equal name (ValueSome "john") "name should match"
        Expect.equal age (ValueSome 30) "age should match"
        Expect.equal status ValueNone "status should not match"
      }

      test
        "UrlMatchExtensions.getParamSeqFromQuery can return a sequence of query params" {
        let template = "/api?name&age<int>&statuses"
        let url = "/api?name=john&age=30&statuses=active&statuses=inactive"

        let _, _, urlMatch =
          RouteMatcher.matchStrings template url
          |> Result.defaultWith(fun e ->
            failtestf "Expected Ok, got Error %A" e
          )

        let name = urlMatch.getFromParams<string>("name")
        let age = urlMatch.getFromParams<int>("age")

        let values = urlMatch.getParamSeqFromQuery<string>("statuses")

        let statuses = values |> ValueOption.defaultValue Seq.empty

        Expect.equal name (ValueSome "john") "name should match"

        Expect.equal age (ValueSome 30) "age should match"

        Expect.sequenceContainsOrder
          statuses
          (seq {
            "inactive"
            "active"
          })
          "statuses should match"

      }
    ]

[<Tests>]
let tests =
  testList "RouteMatcher Tests" [
    Params.tests
    QueryParams.tests
    MatchStrings.tests
  ]
