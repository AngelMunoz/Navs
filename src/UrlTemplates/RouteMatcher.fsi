namespace UrlTemplates.RouteMatcher

open System.Collections.Generic
open FsToolkit.ErrorHandling

open UrlTemplates.UrlTemplate
open UrlTemplates.UrlParser
open System.Runtime.CompilerServices

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
    Params: IReadOnlyDictionary<string, obj>
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
    QueryParams: IReadOnlyDictionary<string, obj>

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
module UrlMatch =

  /// <summary>
  /// Gets a sequence of parameters from the supplied query key in the URL
  /// </summary>
  /// <param name="name">The name of the parameter to get</param>
  /// <param name="urlMatch">The match result to get the parameter from</param>
  /// <returns>
  /// The parameter values if it exists in the query parameters and it was succesfully parsed to it's supplied type or None if it doesn't
  /// </returns>
  val getParamSeqFromQuery<'CastedType> : name: string -> urlMatch: UrlMatch -> 'CastedType seq voption


  /// <summary>
  /// Gets a parameter from the query parameters of the URL
  /// </summary>
  /// <param name="name">The name of the parameter to get</param>
  /// <param name="urlMatch">The match result to get the parameter from</param>
  /// <returns>
  /// The parameter value if it exists in the query parameters and it was succesfully parsed to it's supplied type or None if it doesn't
  /// </returns>
  val getParamFromQuery<'CastedType> : name: string -> urlMatch: UrlMatch -> 'CastedType voption

  /// <summary>
  /// Gets a parameter from the path segments of the URL
  /// </summary>
  /// <param name="name">The name of the parameter to get</param>
  /// <param name="urlMatch">The match result to get the parameter from</param>
  /// <returns>
  /// The parameter value if it exists in the path segments and it was succesfully parsed to it's supplied type or None if it doesn't
  /// </returns>
  val getParamFromPath<'CastedType> : name: string -> urlMatch: UrlMatch -> 'CastedType voption

  /// <summary>
  /// Gets a parameter from the query parameters or segments of the URL
  /// </summary>
  /// <param name="name">The name of the parameter to get</param>
  /// <param name="urlMatch">The match result to get the parameter from</param>
  /// <returns>
  /// The parameter value if it exists in the query parameters or path segments and it was succesfully parsed to it's supplied type or None if it doesn't
  /// </returns>
  val getFromParams<'CastedType> : name: string -> urlMatch: UrlMatch -> 'CastedType voption

[<Class; Sealed; Extension>]
type UrlMatchExtensions =

  /// <summary>
  /// Gets a sequence of parameters from the supplied query key in the URL
  /// </summary>
  /// <param name="name">The name of the parameter to get</param>
  /// <param name="urlMatch">The match result to get the parameter from</param>
  /// <returns>
  /// The parameter values if it exists in the query parameters and it was succesfully parsed to it's supplied type or None if it doesn't
  /// </returns>
  [<Extension; CompiledName "GetParamSeqFromQuery">]
  static member inline getParamSeqFromQuery<'CastedType> : urlMatch: UrlMatch * name: string -> 'CastedType seq voption

  /// <summary>
  /// Gets a parameter from the query parameters of the URL
  /// </summary>
  /// <param name="name">The name of the parameter to get</param>
  /// <param name="urlMatch">The match result to get the parameter from</param>
  /// <returns>
  /// The parameter value if it exists in the query parameters and it was succesfully parsed to it's supplied type or None if it doesn't
  /// </returns>
  [<Extension; CompiledName "GetParamFromQuery">]
  static member inline getParamFromQuery<'CastedType> : urlMatch: UrlMatch * name: string -> 'CastedType voption

  /// <summary>
  /// Gets a parameter from the path segments of the URL
  /// </summary>
  /// <param name="name">The name of the parameter to get</param>
  /// <param name="urlMatch">The match result to get the parameter from</param>
  /// <returns>
  /// The parameter value if it exists in the path segments and it was succesfully parsed to it's supplied type or None if it doesn't
  /// </returns>
  [<Extension; CompiledName "GetParamFromPath">]
  static member inline getParamFromPath<'CastedType> : urlMatch: UrlMatch * name: string -> 'CastedType voption

  /// <summary>
  /// Gets a parameter from the query parameters or segments of the URL
  /// </summary>
  /// <param name="name">The name of the parameter to get</param>
  /// <param name="urlMatch">The match result to get the parameter from</param>
  /// <returns>
  /// The parameter value if it exists in the query parameters or path segments and it was succesfully parsed to it's supplied type or None if it doesn't
  /// </returns>
  [<Extension; CompiledName "GetFromParams">]
  static member inline getFromParams<'CastedType> : urlMatch: UrlMatch * name: string -> 'CastedType voption

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
