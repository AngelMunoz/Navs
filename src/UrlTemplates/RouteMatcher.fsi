namespace UrlTemplates.RouteMatcher

open System.Collections.Generic
open FsToolkit.ErrorHandling

open UrlTemplates.UrlTemplate
open UrlTemplates.UrlParser

type QueryParamError =
  | MissingRequired of string
  | UnparsableQueryItem of name: string * expectedType: TypedParam * value: string
  | UnparsableQueryItems of (string * TypedParam * string) list

/// The error that can be returned when the URL doesn't match the template
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

/// The result of a successful match between a URL and a templated URL
type UrlMatch =
  {
    /// <summary>
    /// The matched segments of the URL, this will be used to extract the parameters
    /// </summary>
    /// <example>
    /// /:id/:name will match /123/john and the segments will be
    ///
    /// Params["id"] = 123
    ///
    /// Params["name"] = "john"
    /// </example>
    Params: Dictionary<string, obj>
    /// <summary>
    /// The matched query parameters of the URL, this will be used to extract the parameters
    /// </summary>
    /// <example>
    /// /users?name&amp;age&lt;int&gt; will match /users?name=john&amp;age=30
    ///
    /// QueryParams["name"] = "john"
    ///
    /// QueryParams["age"] = 30
    /// </example>
    QueryParams: Dictionary<string, obj>

    /// <summary>
    /// The hash of the URL
    /// </summary>
    /// <example>
    /// /users#123 will match /users#123
    ///
    /// Hash = ValueSome "123"
    /// </example>
    Hash: string voption
  }

[<RequireQualifiedAccess>]
module RouteMatcher =

  /// <summary>
  /// Matches a URL against a template
  /// </summary>
  /// <param name="template">The template to match the URL against</param>
  /// <param name="url">The URL to match</param>
  /// <returns>
  /// A validation that contains the matched URL and the match result if the URL matches the template
  /// or a list of MatchingErrors if the URL doesn't match the template.
  /// </returns>
  val matchTemplate: template: UrlTemplate -> url: UrlInfo -> Validation<UrlMatch, MatchingError>

  /// <summary>
  /// Matches a URL against a template
  /// </summary>
  /// <param name="template">The template to match the URL against</param>
  /// <param name="url">The URL to match</param>
  /// <returns>
  /// A validation that contains the matched URL and the match result if the URL matches the template
  /// or a list of MatchingErrors if the URL doesn't match the template.
  /// </returns>
  val matchUrl: template: UrlTemplate -> url: string -> Validation<UrlInfo * UrlMatch, StringMatchError>

  /// <summary>
  /// Matches a URL against a templated string
  /// </summary>
  /// <param name="template">The template to match the URL against</param>
  /// <param name="url">The URL to match</param>
  /// <returns>
  /// A validation that contains the UrlTemplate obtained from the provided string, the matched URL, and the match result if the URL matches the template
  /// or a list of StringMatchErrors if there were problems parsing the template, the URL or if the URL doesn't match the template.
  /// </returns>
  val matchStrings: template: string -> url: string -> Validation<UrlTemplate * UrlInfo * UrlMatch, StringMatchError>
