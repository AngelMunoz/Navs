namespace UrlTemplates.UrlParser

open System
open FParsec
open UrlTemplates.Parser
open FsToolkit.ErrorHandling
open System.Collections.Generic

[<Struct>]
type QueryValue =
  | String of value: string voption
  | StringValues of values: string list

[<Struct>]
type UrlComponent =
  | Segment of segment: string
  | Query of query: Dictionary<string, QueryValue>
  | Hash of hash: string

type UrlInfo = {
  Segments: string list
  Query: Dictionary<string, QueryValue>
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

        StringValues [
          nextValue |> Option.defaultValue ""
          a |> ValueOption.defaultValue ""
        ]

      | Some(StringValues values) ->
        StringValues((nextValue |> Option.defaultValue "") :: values)
      | None -> String(nextValue |> ValueOption.ofOption)

    let tupleListToMap
      (current: Dictionary<string, QueryValue>)
      (nextKey: string, nextValue: string option)
      =
      match current.TryGetValue nextKey with
      | true, existing ->
        current[nextKey] <- addOrUpdate nextValue (Some existing)
        current
      | false, _ ->
        current[nextKey] <- addOrUpdate nextValue None
        current

    sepEndBy queryKv Common.QuerySeparator
    >>= (fun values ->
      values |> List.fold tupleListToMap (Dictionary<_, _>()) |> preturn
    )

  let parse =
    segments
    .>>. opt(Common.QueryMarker >>. query)
    .>>. opt(Common.HashMarker >>. hash)
    .>> eof
    >>= (fun ((segments, query), hash) ->
      let query = query |> Option.defaultValue(Dictionary())

      {
        Segments =
          match segments with
          | [ ""; "" ] -> [ "" ]
          | _ -> segments
        Query = query
        Hash = hash |> ValueOption.ofOption
      }
      |> preturn
    )

  let ofUri (uri: Uri) =
    let uri =
      if uri.IsAbsoluteUri then
        uri
      else
        Uri(
          Uri("url+templates://"),
          Uri($"{uri.PathAndQuery}{uri.Fragment}", UriKind.Relative)
        )

    $"{uri.PathAndQuery}{uri.Fragment}"

  let ofString (url: string) =
    match run parse url with
    | Success(result, _, _) -> Result.Ok result
    | Failure(err, _, _) -> Result.Error(err)
