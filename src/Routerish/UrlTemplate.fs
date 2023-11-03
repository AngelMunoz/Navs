namespace Routerish.UrlTemplate

open FParsec
open Routerish.Parser
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
    let intParam: Parser<TypedParam, unit> =
      pstringCI "<int>" <|> pstringCI "<integer>" >>= (fun _ -> preturn Int)

    let floatParam: Parser<TypedParam, unit> =
      pstringCI "<float>" <|> pstringCI "<number>" >>= (fun _ -> preturn Float)

    let boolParam: Parser<TypedParam, unit> =
      pstringCI "<bool>" <|> pstringCI "<boolean>" >>= (fun _ -> preturn Bool)

    let guidParam: Parser<TypedParam, unit> =
      pstringCI "<guid>" >>= (fun _ -> preturn Guid)

    let longParam: Parser<TypedParam, unit> =
      pstringCI "<long>" >>= (fun _ -> preturn Long)

    let decimalParam: Parser<TypedParam, unit> =
      pstringCI "<decimal>" >>= (fun _ -> preturn Decimal)

  let typed =
    Primitives.intParam
    <|> Primitives.floatParam
    <|> Primitives.boolParam
    <|> Primitives.guidParam
    <|> Primitives.longParam
    <|> Primitives.decimalParam

  let required = pchar '!'

  // "/segment/" matches "segment"
  let plainSegment =
    manyChars(noneOf Common.SegmentDelimiters)
    >>= (fun value -> Plain value |> preturn)

  // ":param/segment" matches "param"
  // "segment/:param<int>/segment" matches "param" and sets its type as an int
  let paramSegment =
    Common.ParamMarker >>. (manyChars asciiLetter) .>>. opt typed
    >>= (fun (name, tipe) ->
      ParamSegment(name, tipe |> Option.defaultValue String) |> preturn
    )

  // "?key&key2!" matches an optional "key" and a required "key2"

  let queryKey =

    (manyChars asciiLetter) .>>. opt typed .>>. opt required
    >>= (fun ((name, tipe), required) ->
      match required with
      | Some _ -> Required(name, tipe |> Option.defaultValue String)
      | None -> Optional(name, tipe |> Option.defaultValue String)
      |> preturn
    )

  let parse =
    sepEndBy (paramSegment <|> plainSegment) Common.SegmentSeparator
    .>>. opt(Common.QueryMarker >>. sepEndBy queryKey Common.QuerySeparator)
    .>>. opt(Common.HashMarker >>. manyChars anyChar)
    .>> eof
    >>= (fun ((segments, query), hash) ->
      let query = query |> Option.defaultValue []

      {
        Segments = segments
        Query = query
        Hash = hash |> ValueOption.ofOption
      }
      |> preturn
    )

  let ofString (template: string) =
    match run parse template with
    | Success(result, _, _) -> Result.Ok result
    | Failure(errorMsg, _, _) -> Result.Error errorMsg
