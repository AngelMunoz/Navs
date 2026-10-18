namespace UrlTemplates.UrlParser

open System
open System.Collections.Generic
open System.Collections.Immutable
open XParsec
open XParsec.Parsers
open XParsec.CharParsers
open UrlTemplates.Parser
open FsToolkit.ErrorHandling

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

  // Internal shape: XParsec's ImmutableArray survives the whole parser.
  // The public UrlInfo holds lists, so conversion happens once in ofString.
  [<Struct>]
  type private RawUrl = {
    Segments: ImmutableArray<string>
    Query: Dictionary<string, QueryValue>
    Hash: string voption
  }

  let private normalizeSegments (segments: ImmutableArray<string>) =
    if segments.Length = 2 && segments[0] = "" && segments[1] = "" then
      ImmutableArray.Create ""
    else
      segments

  let segments: Parser<ImmutableArray<string>, char, unit, ReadableString> =
    sepEndBy
      (manyChars(noneOf Common.SegmentDelimiters))
      Common.SegmentSeparator
    |>> (fun (struct (segments, _)) -> segments)

  let hash: Parser<string, char, unit, ReadableString> = manyChars anyChar

  let queryKv
    : Parser<struct (string * string voption), char, unit, ReadableString> =
    (manyChars(noneOf Common.QueryDelimiters) .>> opt(pchar '='))
    .>>. opt(manyChars(noneOf Common.QueryDelimiters))

  let private addOrUpdate (nextValue: string voption) existing =
    match existing with
    | ValueSome(String a) ->

      StringValues [
        nextValue |> ValueOption.defaultValue ""
        a |> ValueOption.defaultValue ""
      ]

    | ValueSome(StringValues values) ->
      StringValues((nextValue |> ValueOption.defaultValue "") :: values)
    | ValueNone -> String nextValue

  let private tupleToMap
    (current: Dictionary<string, QueryValue>)
    (struct (nextKey, nextValue): struct (string * string voption))
    =
    match current.TryGetValue nextKey with
    | true, existing ->
      current[nextKey] <- addOrUpdate nextValue (ValueSome existing)
      current
    | false, _ ->
      current[nextKey] <- addOrUpdate nextValue ValueNone
      current

  let query: Parser<Dictionary<string, QueryValue>, char, unit, ReadableString> =
    sepEndBy queryKv Common.QuerySeparator
    |>> (fun (struct (pairs, _)) ->
      pairs |> Seq.fold tupleToMap (Dictionary<_, _>())
    )

  let private parse: Parser<RawUrl, char, unit, ReadableString> =
    segments
    .>>. opt(Common.QueryMarker >>. query)
    .>>. opt(Common.HashMarker >>. hash)
    .>> eof
    |>> (fun (struct (struct (segments, query), hash)) ->
      let raw: RawUrl = {
        Segments = normalizeSegments segments
        Query =
          match query with
          | ValueSome query -> query
          | ValueNone -> Dictionary<string, QueryValue>()
        Hash = hash
      }

      raw
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

  let decode (value: string) =
    try
      Uri.UnescapeDataString value
    with _ ->
      value

  let decodeQueryValue =
    function
    | String ValueNone -> String ValueNone
    | String(ValueSome value) ->
      String(ValueSome(decode(value.Replace("+", " "))))
    | StringValues values ->
      StringValues(
        values |> List.map(fun value -> decode(value.Replace("+", " ")))
      )

  let ofString (url: string) =
    match parse(Reader.ofString url ()) with
    | Ok raw ->
      let query = Dictionary<string, QueryValue>()

      for pair in raw.Query do
        let key = decode(pair.Key.Replace("+", " "))
        query[key] <- decodeQueryValue pair.Value

      let info: UrlInfo = {
        Segments = raw.Segments |> Seq.map decode |> List.ofSeq
        Query = query
        Hash = raw.Hash |> ValueOption.map decode
      }

      Result.Ok info
    | Error error -> Result.Error(Report.describe url error)
