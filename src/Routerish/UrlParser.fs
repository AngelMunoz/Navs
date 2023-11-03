namespace Routerish.UrlParser

open System
open FParsec
open Routerish.Parser
open FsToolkit.ErrorHandling

[<Struct>]
type QueryValue =
  | String of value: string voption
  | StringValues of values: string list

[<Struct>]
type UrlComponent =
  | Segment of segment: string
  | Query of query: Map<string, QueryValue>
  | Hash of hash: string

type UrlInfo = {
  Segments: string list
  Query: Map<string, QueryValue>
  Hash: string voption
}

[<RequireQualifiedAccess>]
module UrlParser =

  let segments =
    sepEndBy
      (manyChars(noneOf Common.SegmentDelimiters))
      Common.SegmentSeparator

  let hash = manyChars anyChar

  let query =
    let queryKv =
      let separator = pchar '='
      let key = manyChars(noneOf Common.QueryDelimiters)
      let value = manyChars(noneOf Common.QueryDelimiters)

      key .>> opt separator .>>. opt value

    let addOrUpdate (nextValue: string option) existing =
      match existing with
      | Some(String a) ->
        Some(
          StringValues [
            nextValue |> Option.defaultValue ""
            a |> ValueOption.defaultValue ""
          ]
        )
      | Some(StringValues values) ->
        Some(StringValues((nextValue |> Option.defaultValue "") :: values))
      | None -> Some(String(nextValue |> ValueOption.ofOption))

    let tupleListToMap current (nextKey: string, nextValue: string option) =
      Map.change nextKey (addOrUpdate nextValue) current

    sepEndBy queryKv Common.QuerySeparator
    >>= (fun values -> values |> List.fold tupleListToMap Map.empty |> preturn)

  let parse =
    segments
    .>>. opt(Common.QueryMarker >>. query)
    .>>. opt(Common.HashMarker >>. hash)
    .>> eof
    >>= (fun ((segments, query), hash) ->
      let query = query |> Option.defaultValue Map.empty

      {
        Segments = segments
        Query = query
        Hash = hash |> ValueOption.ofOption
      }
      |> preturn
    )

  let FromUri (uri: Uri) =
    let uri =
      if uri.IsAbsoluteUri then
        uri
      else
        Uri(
          Uri("http://localhost.com"),
          Uri(uri.OriginalString, UriKind.Relative)
        )

    $"{uri.PathAndQuery[1..]}{uri.Fragment}"

  let ofString (url: string) =
    match run parse url with
    | Success(result, _, _) -> Result.Ok result
    | Failure(err, _, _) -> Result.Error(err)
