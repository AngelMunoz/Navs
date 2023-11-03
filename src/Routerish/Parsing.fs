[<RequireQualifiedAccess>]
module Routerish.Parser.Common

open FParsec


let SegmentDelimiters = [ '/'; '#'; '?' ]

let QueryDelimiters = [ '='; '&'; '#' ]

let QuerySeparator: Parser<char, unit> = pchar '&'

let SegmentSeparator: Parser<char, unit> = pchar '/'


let ParamMarker: Parser<char, unit> = pchar ':'

let QueryMarker: Parser<char, unit> = pchar '?'

let HashMarker: Parser<char, unit> = pchar '#'
