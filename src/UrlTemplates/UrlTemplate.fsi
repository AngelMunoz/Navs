namespace UrlTemplates.UrlTemplate

/// A few well known types that can be used as as the types for the parameters of a URL.
[<Struct>]
type TypedParam =
  | String
  | Int
  | Float
  | Bool
  | Guid
  | Long
  | Decimal

/// A discriminated union that represents the different types of segments that a URL can have
[<Struct>]
type TemplateSegment =
  /// <summary>
  /// A segment of the URL that is a constant string
  /// </summary>
  /// <example>
  /// /users/disable will have the segments
  ///
  /// [ Plain "users"; Plain "disable" ]
  /// </example>
  | Plain of string
  /// <summary>
  /// A segment of the URL that is a parameter
  /// </summary>
  /// <example>
  /// /users/:id will have the segments
  ///
  /// [ Plain "users"; ParamSegment ("id", String) ]
  /// </example>
  | ParamSegment of name: string * tipe: TypedParam

/// A discriminated union that represents the different types of query parameters that a URL can have
[<Struct>]
type QueryKey =
  /// <summary>
  /// A query parameter that is required in the URL
  /// </summary>
  /// <example>
  /// /?name! means that the URL must have the query parameter "name"
  /// Required("name", String)
  /// </example>
  | Required of reqName: string * reqTipe: TypedParam
  /// <summary>
  /// A query parameter that is optional in the URL
  /// </summary>
  /// <example>
  /// /?name means that the URL can have the query parameter "name"
  /// Optional("name", String)
  /// </example>
  | Optional of name: string * tipe: TypedParam

/// A discriminated union that represents the different types of components that a URL can have
[<Struct>]
type TemplateComponent =
  | Segment of segment: TemplateSegment
  | Query of query: QueryKey list
  | Hash of hash: string voption

/// <summary>
/// This object is the representation of an URL template including
/// the segments, query parameters and hash and its corresponding types
/// in the case of the segments and query parameters
/// </summary>
[<Struct>]
type UrlTemplate =
  { Segments: TemplateSegment list
    Query: QueryKey list
    Hash: string voption }

[<RequireQualifiedAccess>]
module UrlTemplate =

  /// <summary>
  /// Parses a string into a URL template
  /// </summary>
  /// <param name="template">The string to parse into a URL template</param>
  /// <returns>
  /// A URL template if the string is a valid URL template
  /// or a string with the error message if the string is not a valid URL template
  /// </returns>
  val ofString: template: string -> Result<UrlTemplate, string>

  /// <summary>
  /// Converts a URL template into a well formed and matching URL
  /// </summary>
  /// <param name="template">The URL template to convert into a URL</param>
  /// <param name="routeParams">The parameters to use to convert the URL template into a URL</param>
  /// <returns>
  /// A string with the URL if the parameters are valid
  /// or a list of string with the error messages if the parameters are not valid
  /// </returns>
  val toUrl:
    template: string ->
    routeParams: System.Collections.Generic.IReadOnlyDictionary<string, obj> ->
      Result<string, string list>
