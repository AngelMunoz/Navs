namespace UrlTemplates.UrlTemplate

open System.Collections.Generic
open System.Collections.Immutable
open XParsec
open XParsec.Parsers
open XParsec.CharParsers
open UrlTemplates.Parser
open FsToolkit.ErrorHandling

[<Struct>]
type TypedParam =
  | String
  | Int
  | Float
  | Bool
  | Guid
  | Long
  | Decimal

[<Struct>]
type TemplateSegment =
  | Plain of string
  | ParamSegment of name: string * tipe: TypedParam

[<Struct>]
type QueryKey =
  | Required of reqName: string * reqTipe: TypedParam
  | Optional of name: string * tipe: TypedParam

[<Struct>]
type TemplateComponent =
  | Segment of segment: TemplateSegment
  | Query of query: QueryKey list
  | Hash of hash: string voption

[<Struct>]
type UrlTemplate = {
  Segments: TemplateSegment list
  Query: QueryKey list
  Hash: string voption
}

[<RequireQualifiedAccess>]
module UrlTemplate =

  module Primitives =
    let intParam: Parser<TypedParam, char, unit, ReadableString> =
      stringCIReturn "<int>" Int <|> stringCIReturn "<integer>" Int

    let floatParam: Parser<TypedParam, char, unit, ReadableString> =
      stringCIReturn "<float>" Float <|> stringCIReturn "<number>" Float

    let boolParam: Parser<TypedParam, char, unit, ReadableString> =
      stringCIReturn "<bool>" Bool <|> stringCIReturn "<boolean>" Bool

    let guidParam: Parser<TypedParam, char, unit, ReadableString> =
      stringCIReturn "<guid>" Guid

    let longParam: Parser<TypedParam, char, unit, ReadableString> =
      stringCIReturn "<long>" Long

    let decimalParam: Parser<TypedParam, char, unit, ReadableString> =
      stringCIReturn "<decimal>" Decimal

  let typed: Parser<TypedParam, char, unit, ReadableString> =
    choice [
      Primitives.intParam
      Primitives.floatParam
      Primitives.boolParam
      Primitives.guidParam
      Primitives.longParam
      Primitives.decimalParam
    ]

  // "/segment/" matches "segment"
  let plainSegment: Parser<TemplateSegment, char, unit, ReadableString> =
    manyChars(noneOf Common.SegmentDelimiters) |>> Plain

  // ":param/segment" matches "param"
  // "segment/:param<int>/segment" matches "param" and sets its type as an int
  let paramSegment: Parser<TemplateSegment, char, unit, ReadableString> =
    (Common.ParamMarker >>. manyChars asciiLetter) .>>. opt typed
    |>> (fun (struct (name, tipe)) ->
      ParamSegment(name, tipe |> ValueOption.defaultValue String)
    )

  // "?key&key2!" matches an optional "key" and a required "key2"
  let queryKey: Parser<QueryKey, char, unit, ReadableString> =
    (manyChars(asciiLetter <|> anyOf [ '-'; '_' ]) .>>. opt typed)
    .>>. opt(pchar '!')
    |>> (fun (struct (struct (name, tipe), required)) ->
      let tipe = tipe |> ValueOption.defaultValue String

      match required with
      | ValueSome _ -> Required(name, tipe)
      | ValueNone -> Optional(name, tipe)
    )

  let private segmentList
    : Parser<ImmutableArray<TemplateSegment>, char, unit, ReadableString> =
    sepEndBy (paramSegment <|> plainSegment) Common.SegmentSeparator
    |>> (fun (struct (segments, _)) -> segments)

  let private queryList
    : Parser<ImmutableArray<QueryKey> voption, char, unit, ReadableString> =
    opt(
      Common.QueryMarker
      >>. (sepEndBy queryKey Common.QuerySeparator
           |>> (fun (struct (query, _)) -> query))
    )

  let private hashValue: Parser<string voption, char, unit, ReadableString> =
    opt(Common.HashMarker >>. manyChars anyChar)

  // Internal shape: XParsec's ImmutableArray survives the whole parser.
  // The public UrlTemplate holds lists, so conversion happens once in ofString.
  [<Struct>]
  type private RawTemplate = {
    Segments: ImmutableArray<TemplateSegment>
    Query: ImmutableArray<QueryKey>
    Hash: string voption
  }

  let private normalizeSegments (segments: ImmutableArray<TemplateSegment>) =
    if segments.Length = 2 then
      match segments[0], segments[1] with
      | Plain "", Plain "" -> ImmutableArray.Create(Plain "")
      | _ -> segments
    else
      segments

  let private parse: Parser<RawTemplate, char, unit, ReadableString> =
    segmentList .>>. queryList .>>. hashValue .>> eof
    |>> (fun (struct (struct (segments, query), hash)) ->
      let raw: RawTemplate = {
        Segments = normalizeSegments segments
        Query =
          match query with
          | ValueSome query -> query
          | ValueNone -> ImmutableArray.Empty
        Hash = hash
      }

      raw
    )

  let ofString (template: string) =
    match parse(Reader.ofString template ()) with
    | Ok raw ->
      let parsed: UrlTemplate = {
        Segments = List.ofSeq raw.Segments
        Query = List.ofSeq raw.Query
        Hash = raw.Hash
      }

      Result.Ok parsed
    | Error error -> Result.Error(Report.describe template error)

  let toUrl (template: string) (routeParams: IReadOnlyDictionary<string, obj>) = result {
    let routeParams =
      routeParams
      |> ValueOption.ofNull
      |> ValueOption.defaultWith(fun _ -> new Dictionary<string, obj>())

    let! parsed = ofString template |> Result.mapError(fun x -> [ x ])

    let! segments =
      parsed.Segments
      |> List.traverseResultA(
        function
        | Plain value -> Result.Ok value
        | ParamSegment(name, _) ->
          match routeParams.TryGetValue name with
          | true, value ->
            match value.ToString() with
            | null ->
              Result.Error $"Required route parameter %s{name} was not provided"
            | value -> Result.Ok value
          | false, _ ->
            Result.Error $"Required route parameter %s{name} was not provided"
      )
      |> Result.map(fun x -> x |> String.concat "/")

    let! query =
      parsed.Query
      |> List.traverseResultA(
        function
        | Required(name, _) ->
          match routeParams.TryGetValue name with
          | true, value -> Result.Ok($"{name}={value.ToString()}")
          | false, _ ->
            Result.Error $"Required route parameter %s{name} was not provided"
        | Optional(name, _) ->
          match routeParams.TryGetValue name with
          | true, value -> Result.Ok($"{name}={value.ToString()}")
          | false, _ -> Result.Ok("")
      )
      |> Result.map(fun x -> x |> String.concat "&")

    let hash =
      match parsed.Hash with
      | ValueSome value -> value
      | ValueNone -> ""

    let sb = System.Text.StringBuilder()

    sb.Append segments |> ignore

    if not(System.String.IsNullOrWhiteSpace query) then
      sb.Append("?").Append query |> ignore

    if not(System.String.IsNullOrWhiteSpace hash) then
      sb.Append("#").Append hash |> ignore

    return sb.ToString()
  }
