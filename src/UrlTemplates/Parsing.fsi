[<RequireQualifiedAccess>]
module internal UrlTemplates.Parser.Common

open FParsec
val SegmentDelimiters: char list
val QueryDelimiters: char list
val QuerySeparator: Parser<char, unit>
val SegmentSeparator: Parser<char, unit>
val ParamMarker: Parser<char, unit>
val QueryMarker: Parser<char, unit>
val HashMarker: Parser<char, unit>
