namespace UrlTemplates.RouteMatcher

open System
open System.Collections.Generic

open FsToolkit.ErrorHandling

open UrlTemplates.UrlTemplate
open UrlTemplates.UrlParser

type QueryParamError =
  | MissingRequired of string
  | UnparsableQueryItem of
    name: string *
    expectedType: TypedParam *
    value: string
  | UnparsableQueryItems of (string * TypedParam * string) list

type MatchingError =
  | SegmentLengthMismatch
  | MissingQueryParams of string list
  | SegmentMismatch of string * string
  | UnparsableParam of name: string * expectedType: TypedParam * value: string
  | QueryParamError of QueryParamError

type StringMatchError =
  | TemplateParsingError of string
  | UrlParsingError of string
  | MatchingError of MatchingError


type UrlMatch = {
  Params: Map<string, obj>
  QueryParams: Map<string, obj>
  Hash: string voption
}

[<RequireQualifiedAccess>]
module RouteMatcher =

  let getKeySize (map: QueryKey list) =
    map
    |> List.fold
      (fun (required, optional) next ->
        match next with
        | Required _ -> (required + 1, optional)
        | Optional _ -> (required, optional + 1)
      )
      (0, 0)

  let tryParseValue (tipe: TypedParam) (value: string) : Result<obj, string> = result {
    match tipe with
    | TypedParam.String -> return value
    | Int ->
      match Int32.TryParse value with
      | true, v -> return v
      | false, _ -> return! Error "Could not parse int"
    | Float ->
      match Double.TryParse value with
      | true, v -> return v
      | false, _ -> return! Error "Could not parse float"
    | Bool ->
      match Boolean.TryParse value with
      | true, v -> return v
      | false, _ -> return! Error "Could not parse bool"
    | Guid ->
      match Guid.TryParse value with
      | true, v -> return v
      | false, _ -> return! Error "Could not parse guid"
    | Long ->
      match Int64.TryParse value with
      | true, v -> return v
      | false, _ -> return! Error "Could not parse long"
    | Decimal ->
      match Decimal.TryParse value with
      | true, v -> return v
      | false, _ -> return! Error "Could not parse decimal"
  }

  let fillParamBag
    (fromTemplate: TemplateSegment list)
    (fromUrl: string list)
    map
    =
    let addToBag
      (templated: TemplateSegment)
      (url: string)
      (bag: Dictionary<string, obj>)
      =
      result {
        match templated with
        | Plain name ->
          if name <> url then
            return! Error(SegmentMismatch(name, url))
          else
            bag.Add(name, url)
            return ()
        | ParamSegment(name, tipe) ->
          let! parsedValue =
            tryParseValue tipe url
            |> Result.mapError(fun _ -> UnparsableParam(name, tipe, url))

          bag.Add(name, parsedValue)
          return ()
      }

    let bag = Dictionary<string, obj>(map :> IDictionary<string, obj>)
    let errors = ResizeArray()

    for index in 0 .. (fromTemplate.Length - 1) do
      let item = fromTemplate[index]
      let url = fromUrl[index]

      match addToBag item url bag with
      | Ok() -> ()
      | Error err -> errors.Add err

    if errors.Count <= 0 then
      bag |> Seq.map (|KeyValue|) |> Map.ofSeq |> Ok
    else
      Error(errors |> List.ofSeq)

  let extractRequired name (value: string voption) tipe map = result {
    let! value =
      match value with
      | ValueSome value -> Ok value
      | ValueNone -> Error(MissingRequired(name))

    let! parsedValue =
      tryParseValue tipe value
      |> Result.mapError(fun _ -> UnparsableQueryItem(name, tipe, value))

    return map |> Map.add name parsedValue
  }

  let extractOptional name (value: string voption) tipe map = result {
    match value with
    | ValueNone -> return map
    | ValueSome value ->
      let! parsedValue =
        tryParseValue tipe value
        |> Result.mapError(fun _ -> UnparsableQueryItem(name, tipe, value))

      return map |> Map.add name parsedValue
  }

  let extractListValues name tipe values (map: Map<string, obj>) =
    match
      values
      |> List.traverseResultA(fun value ->
        tryParseValue tipe value |> Result.mapError(fun _ -> name, tipe, value)
      )
    with
    | Ok values -> Ok(map |> Map.add name values)
    | Error errs -> errs |> UnparsableQueryItems |> Error

  let fillQueryParams
    (templated: QueryKey list)
    (url: Map<string, QueryValue>)
    (bag: Map<string, obj>)
    =
    templated
    |> List.fold
      (fun current next -> result {
        let! current = current

        match next with
        | Required(name, tipe) ->
          match url |> Map.tryFind name with
          | None -> return! Error(MissingRequired(name))
          | Some(String value) ->
            return! extractRequired name value tipe current
          | Some(StringValues values) ->
            return! extractListValues name tipe values current
        | Optional(name, tipe) ->
          match url |> Map.tryFind name with
          | None -> return current
          | Some(String value) ->
            return! extractOptional name value tipe current
          | Some(StringValues values) ->
            return! extractListValues name tipe values current
      })
      (Ok bag)

  let inline getMissingQueryParams url key =
    match key with
    | Required(name, _) ->
      match url.Query |> Map.tryFind name with
      | None -> Some name
      | Some _ -> None
    | Optional(_, _) -> None

  let collectMissingQueryParams (template: UrlTemplate) url _ =
    template.Query
    |> List.choose(getMissingQueryParams url)
    |> MissingQueryParams

  let matchTemplate (template: UrlTemplate) (url: UrlInfo) = validation {
    let requiredKeysSize, _ = getKeySize template.Query
    let urlKeySize = url.Query |> Map.keys |> Seq.length

    do!
      template.Segments.Length = url.Segments.Length
      |> Result.requireTrue SegmentLengthMismatch

    do!
      urlKeySize >= requiredKeysSize
      |> Result.requireTrue ""
      |> Result.mapError(collectMissingQueryParams template url)

    let! urlParams = fillParamBag template.Segments url.Segments Map.empty

    and! queryParamsBag =
      fillQueryParams template.Query url.Query Map.empty
      |> Result.mapError QueryParamError

    return {
      Params = urlParams
      QueryParams = queryParamsBag
      Hash = url.Hash
    }
  }

  let matchStrings template url = validation {
    let! template =
      UrlTemplate.ofString template |> Result.mapError TemplateParsingError

    and! url = UrlParser.ofString url |> Result.mapError UrlParsingError

    let! value = matchTemplate template url |> Validation.mapError MatchingError
    return template, url, value
  }

  let matchUrl template url = validation {
    let! url = UrlParser.ofString url |> Result.mapError UrlParsingError

    let! value = matchTemplate template url |> Validation.mapError MatchingError
    return url, value
  }
