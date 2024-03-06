namespace UrlTemplates.UrlParser

open System
open System.Collections.Generic

/// URL query values can be either a single string or a list of strings
/// This is used to represent the query parameters of a URL
[<Struct>]
type QueryValue =
  | String of value: string voption
  | StringValues of values: string list

/// A URL component can be a segment, a query or a hash
/// Segments are what is commonly known as the "path" of the URL
/// Query is the part of the URL that comes after the "?" and is usually
/// in the form of "key=value", although it can also be in the form of "key"
/// Hash is the part of the URL that comes after the "#"
[<Struct>]
type UrlComponent =
  | Segment of segment: string
  | Query of query: Dictionary<string, QueryValue>
  | Hash of hash: string

/// The result of parsing a URL
/// This is used to represent the different components of a URL
/// Since we're just parsing the string, all of the values are strings
type UrlInfo =
  {
    /// <summary>
    /// The segments of the
    /// </summary>
    /// <example>
    /// /users/123 will have the segments
    ///
    /// [ "users"; "123" ]
    /// </example>
    Segments: string list

    /// <summary>
    /// The query parameters of the URL
    /// </summary>
    /// <example>
    /// /users?name&amp;age&lt;int&gt;&amp;optional will have the query parameters
    ///
    /// {
    ///   { "name", String "john" }
    ///   { "age", String "30" }
    ///   { "optional", ValueNone }
    /// }
    /// </example>
    Query: Dictionary<string, QueryValue>

    /// <summary>
    /// The hash of the URL
    /// </summary>
    /// <example>
    /// /users#123 will have the hash
    ///
    /// ValueSome "123"
    /// </example>
    Hash: string voption
  }

[<RequireQualifiedAccess>]
module UrlParser =

  /// <summary>
  /// Takes an existing URI and returns a string representation of it
  /// </summary>
  /// <param name="uri">The URI to convert to a string</param>
  /// <returns>
  /// A string representation of the URI which can then be used to parse the URL
  /// </returns>
  /// <remarks>
  /// This is particularly useful to enable deep linking in an application.
  /// You'd use this function to convert a URI to a string and then use the string
  /// </remarks>
  val ofUri: uri: Uri -> string

  /// <summary>
  /// Parses a URL and returns the different components of it
  /// </summary>
  /// <param name="url">The URL to parse</param>
  /// <returns>
  /// A result that contains the different components of the URL if the URL is valid
  /// or a string with the error message if the URL is invalid
  /// </returns>
  val ofString: url: string -> Result<UrlInfo, string>
