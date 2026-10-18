namespace UrlTemplates.CSharp;

/// <summary>The reason a template or URL did not match.</summary>
public enum MatchErrorKind
{
    /// <summary>The template text is not a valid route template.</summary>
    TemplateParsing,

    /// <summary>The URL text is not a valid URL.</summary>
    UrlParsing,

    /// <summary>The template and the URL declare a different number of segments.</summary>
    SegmentLengthMismatch,

    /// <summary>The URL omits query parameters the template marks as required.</summary>
    MissingQueryParams,

    /// <summary>A literal template segment and the URL segment differ.</summary>
    SegmentMismatch,

    /// <summary>A path parameter does not parse as its declared type.</summary>
    UnparsableParam,

    /// <summary>A query parameter the template marks as required is absent.</summary>
    QueryParameterMissing,

    /// <summary>A query parameter does not parse as its declared type.</summary>
    QueryParameterUnparsable,

    /// <summary>Several repeated query values do not parse as their declared type.</summary>
    QueryParametersUnparsable
}

/// <summary>One query value that did not parse as its declared type.</summary>
public sealed class UnparsableItem
{
    public string Name { get; }

    public ParameterType Expected { get; }

    public string Value { get; }

    internal UnparsableItem(string name, ParameterType expected, string value)
    {
        Name = name;
        Expected = expected;
        Value = value;
    }
}

/// <summary>A structured match failure. Only the fields that apply to <see cref="Kind"/> carry a value.</summary>
public sealed class MatchError
{
    public MatchErrorKind Kind { get; }

    /// <summary>The parser message for <see cref="MatchErrorKind.TemplateParsing"/> and <see cref="MatchErrorKind.UrlParsing"/>.</summary>
    public string? Message { get; init; }

    /// <summary>The missing query parameter names.</summary>
    public IReadOnlyList<string> Missing { get; init; } = Array.Empty<string>();

    /// <summary>The literal template segment, for <see cref="MatchErrorKind.SegmentMismatch"/>.</summary>
    public string? TemplateSegment { get; init; }

    /// <summary>The URL segment, for <see cref="MatchErrorKind.SegmentMismatch"/>.</summary>
    public string? UrlSegment { get; init; }

    /// <summary>The parameter name, for the unparsable cases.</summary>
    public string? Name { get; init; }

    /// <summary>The declared parameter type, for the unparsable cases.</summary>
    public ParameterType? Expected { get; init; }

    /// <summary>The offending value, for the single-value unparsable cases.</summary>
    public string? Value { get; init; }

    /// <summary>The offending values, for <see cref="MatchErrorKind.QueryParametersUnparsable"/>.</summary>
    public IReadOnlyList<UnparsableItem> Unparsable { get; init; } = Array.Empty<UnparsableItem>();

    public MatchError(MatchErrorKind kind) => Kind = kind;
}
