namespace UrlTemplates.CSharp;

/// <summary>Parses and builds URL templates without any F# types in the signatures.</summary>
public static class UrlTemplate
{
    private static readonly IReadOnlyDictionary<string, object> NoParameters =
        new Dictionary<string, object>();

    /// <summary>Parses a route template such as <c>/users/:id&lt;int&gt;</c>.</summary>
    public static bool TryParse(string pattern, out Template? template, out string? error)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var result = CoreTemplateModule.ofString(pattern);

        if (result.IsOk)
        {
            template = Mapping.ToTemplate(result.ResultValue);
            error = null;
            return true;
        }

        template = null;
        error = result.ErrorValue;
        return false;
    }

    /// <summary>Builds a URL from a template and its parameters.</summary>
    public static bool TryBuild(
        string pattern,
        IReadOnlyDictionary<string, object>? parameters,
        out string? url,
        out IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var result = CoreTemplateModule.toUrl(pattern, parameters ?? NoParameters);

        if (result.IsOk)
        {
            url = result.ResultValue;
            errors = Array.Empty<string>();
            return true;
        }

        url = null;
        errors = result.ErrorValue.ToArray();
        return false;
    }
}
