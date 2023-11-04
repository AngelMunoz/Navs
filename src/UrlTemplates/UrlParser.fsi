namespace UrlTemplates.UrlParser

open System
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

type UrlInfo =
  { Segments: string list
    Query: Dictionary<string, QueryValue>
    Hash: string voption }

[<RequireQualifiedAccess>]
module UrlParser =
  val ofUri: uri: Uri -> string
  val ofString: url: string -> Result<UrlInfo, string>
