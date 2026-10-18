namespace UrlTemplates.CSharp;

/// <summary>Parses URLs and matches them against templates.</summary>
public static class RouteMatcher
{
    /// <summary>Parses a URL into its segments, query and hash.</summary>
    public static bool TryParseUrl(string url, out Url? parsed, out string? error)
    {
        ArgumentNullException.ThrowIfNull(url);

        var result = CoreUrlParserModule.ofString(url);

        if (result.IsOk)
        {
            parsed = Mapping.ToUrl(result.ResultValue);
            error = null;
            return true;
        }

        parsed = null;
        error = result.ErrorValue;
        return false;
    }

    /// <summary>Parses the template and the URL, then matches them.</summary>
    public static bool TryMatch(
        string pattern,
        string url,
        out RouteMatch? match,
        out IReadOnlyList<MatchError> errors)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(url);

        var result = CoreRouteMatcherModule.matchStrings(pattern, url);

        if (result.IsOk)
        {
            match = Mapping.ToRouteMatch(result.ResultValue.Item3);
            errors = Array.Empty<MatchError>();
            return true;
        }

        match = null;
        errors = Mapping.ToMatchErrors(result.ErrorValue);
        return false;
    }

    /// <summary>Matches a URL against an already parsed template.</summary>
    public static bool TryMatch(
        Template template,
        string url,
        out RouteMatch? match,
        out IReadOnlyList<MatchError> errors)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(url);

        var result = CoreRouteMatcherModule.matchUrl(template.Value, url);

        if (result.IsOk)
        {
            match = Mapping.ToRouteMatch(result.ResultValue.Item2);
            errors = Array.Empty<MatchError>();
            return true;
        }

        match = null;
        errors = Mapping.ToMatchErrors(result.ErrorValue);
        return false;
    }

    /// <summary>Matches a parsed URL against an already parsed template.</summary>
    public static bool TryMatch(
        Template template,
        Url url,
        out RouteMatch? match,
        out IReadOnlyList<MatchError> errors)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(url);

        var result = CoreRouteMatcherModule.matchTemplate(template.Value, url.Value);

        if (result.IsOk)
        {
            match = Mapping.ToRouteMatch(result.ResultValue);
            errors = Array.Empty<MatchError>();
            return true;
        }

        match = null;
        errors = Mapping.ToMatchErrors(result.ErrorValue);
        return false;
    }
}
