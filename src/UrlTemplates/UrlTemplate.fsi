namespace UrlTemplates.UrlTemplate

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
type UrlTemplate =
  { Segments: TemplateSegment list
    Query: QueryKey list
    Hash: string voption }

[<RequireQualifiedAccess>]
module UrlTemplate =

  val ofString: template: string -> Result<UrlTemplate, string>
