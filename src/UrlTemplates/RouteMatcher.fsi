namespace UrlTemplates.RouteMatcher

open FsToolkit.ErrorHandling

open UrlTemplates.UrlTemplate
open UrlTemplates.UrlParser

type QueryParamError =
  | MissingRequired of string
  | UnparsableQueryItem of name: string * expectedType: TypedParam * value: string
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

type UrlMatch =
  { Params: Map<string, obj>
    QueryParams: Map<string, obj>
    Hash: string voption }

[<RequireQualifiedAccess>]
module RouteMatcher =

  val matchTemplate: template: UrlTemplate -> url: UrlInfo -> Validation<UrlMatch, MatchingError>

  val matchUrl: template: UrlTemplate -> url: string -> Validation<UrlInfo * UrlMatch, StringMatchError>

  val matchStrings: template: string -> url: string -> Validation<UrlTemplate * UrlInfo * UrlMatch, StringMatchError>
