namespace UrlTemplates.CSharp;

internal static class Mapping
{
    public static Template ToTemplate(CoreTemplate value) =>
        new(
            value,
            value.Segments.Select(ToSegment).ToArray(),
            value.Query.Select(ToQueryKey).ToArray(),
            ToNullable(value.Hash));

    public static Segment ToSegment(CoreTemplateSegment value) =>
        value.IsPlain
            ? new Segment(SegmentKind.Plain, value.Item, null, null)
            : new Segment(SegmentKind.Parameter, null, value.name, ToParameterType(value.tipe));

    public static QueryKey ToQueryKey(CoreQueryKey value) =>
        value.IsRequired
            ? new QueryKey(true, value.reqName!, ToParameterType(value.reqTipe))
            : new QueryKey(false, value.name!, ToParameterType(value.tipe));

    public static Url ToUrl(CoreUrlInfo value) =>
        new(
            value,
            value.Segments.ToArray(),
            value.Query.ToDictionary(pair => pair.Key, pair => ToQueryValue(pair.Value)),
            ToNullable(value.Hash));

    public static UrlQueryValue ToQueryValue(CoreQueryValue value) =>
        value.IsString
            ? new UrlQueryValue(QueryValueKind.Single, ToNullable(value.value), Array.Empty<string>())
            : new UrlQueryValue(
                QueryValueKind.Multiple,
                null,
                value.values!.Select(item => item!).ToArray());

    public static RouteMatch ToRouteMatch(CoreUrlMatch value) =>
        new(value.Params, value.QueryParams, ToNullable(value.Hash));

    public static ParameterType ToParameterType(CoreParameterType value)
    {
        if (value.IsString) { return ParameterType.String; }

        if (value.IsInt) { return ParameterType.Int; }

        if (value.IsFloat) { return ParameterType.Float; }

        if (value.IsBool) { return ParameterType.Bool; }

        if (value.IsGuid) { return ParameterType.Guid; }

        if (value.IsLong) { return ParameterType.Long; }

        if (value.IsDecimal) { return ParameterType.Decimal; }

        throw new NotSupportedException($"Unsupported parameter type: {value}");
    }

    public static IReadOnlyList<MatchError> ToMatchErrors(
        Microsoft.FSharp.Collections.FSharpList<CoreStringMatchError> errors) =>
        errors.Select(ToMatchError).ToArray();

    public static IReadOnlyList<MatchError> ToMatchErrors(
        Microsoft.FSharp.Collections.FSharpList<CoreMatchingError> errors) =>
        errors.Select(ToMatchError).ToArray();

    public static MatchError ToMatchError(CoreStringMatchError error) =>
        error switch
        {
            CoreStringMatchError.TemplateParsingError value =>
                new MatchError(MatchErrorKind.TemplateParsing) { Message = value.Item },
            CoreStringMatchError.UrlParsingError value =>
                new MatchError(MatchErrorKind.UrlParsing) { Message = value.Item },
            CoreStringMatchError.MatchingError value => ToMatchError(value.Item),
            _ => throw new NotSupportedException($"Unsupported match error: {error}")
        };

    public static MatchError ToMatchError(CoreMatchingError error)
    {
        switch (error)
        {
            case CoreMatchingError.MissingQueryParams value:
                return new MatchError(MatchErrorKind.MissingQueryParams)
                {
                    Missing = value.Item.ToArray()
                };
            case CoreMatchingError.SegmentMismatch value:
                return new MatchError(MatchErrorKind.SegmentMismatch)
                {
                    TemplateSegment = value.Item1,
                    UrlSegment = value.Item2
                };
            case CoreMatchingError.UnparsableParam value:
                return new MatchError(MatchErrorKind.UnparsableParam)
                {
                    Name = value.name,
                    Expected = ToParameterType(value.expectedType),
                    Value = value.value
                };
            case CoreMatchingError.QueryParamError value:
                return ToMatchError(value.Item);
        }

        // SegmentLengthMismatch carries no fields, so F# compiles it as a singleton
        // value instead of a case type and the switch above cannot match it.
        if (error.IsSegmentLengthMismatch)
        {
            return new MatchError(MatchErrorKind.SegmentLengthMismatch);
        }

        throw new NotSupportedException($"Unsupported matching error: {error}");
    }

    public static MatchError ToMatchError(CoreQueryParamError error) =>
        error switch
        {
            CoreQueryParamError.MissingRequired value =>
                new MatchError(MatchErrorKind.QueryParameterMissing) { Missing = new[] { value.Item } },
            CoreQueryParamError.UnparsableQueryItem value =>
                new MatchError(MatchErrorKind.QueryParameterUnparsable)
                {
                    Name = value.name,
                    Expected = ToParameterType(value.expectedType),
                    Value = value.value
                },
            CoreQueryParamError.UnparsableQueryItems value =>
                new MatchError(MatchErrorKind.QueryParametersUnparsable)
                {
                    Unparsable = value.Item.Select(ToUnparsableItem).ToArray()
                },
            _ => throw new NotSupportedException($"Unsupported query parameter error: {error}")
        };

    private static UnparsableItem ToUnparsableItem(
        Tuple<string, CoreParameterType, string> item) =>
        new(item.Item1, ToParameterType(item.Item2), item.Item3);

    private static string? ToNullable(Microsoft.FSharp.Core.FSharpValueOption<string> value) =>
        value.IsValueSome ? value.Value : null;
}
