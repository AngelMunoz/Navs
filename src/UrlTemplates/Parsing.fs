namespace UrlTemplates.Parser

open XParsec
open XParsec.CharParsers

[<RequireQualifiedAccess>]
module internal Common =

  let SegmentDelimiters = [ '/'; '#'; '?' ]

  let QueryDelimiters = [ '='; '&'; '#' ]

  let QuerySeparator: Parser<char, char, unit, ReadableString> = pchar '&'

  let SegmentSeparator: Parser<char, char, unit, ReadableString> = pchar '/'

  let ParamMarker: Parser<char, char, unit, ReadableString> = pchar ':'

  let QueryMarker: Parser<char, char, unit, ReadableString> = pchar '?'

  let HashMarker: Parser<char, char, unit, ReadableString> = pchar '#'

[<RequireQualifiedAccess>]
module internal Report =

  /// Renders a parse failure without depending on the parser library's wording, so
  /// the message stays stable if the engine changes again.
  let describe (input: string) (error: ParseError<char, 'State>) =
    let index = error.Position.Index

    if index >= 0 && index < input.Length then
      $"unexpected '{input[index]}' at index {index}"
    else
      $"unexpected end of input at index {index}"
