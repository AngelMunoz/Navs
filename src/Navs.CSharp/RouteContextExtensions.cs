using Core = Navs;

namespace Navs.CSharp;

/// <summary>Typed parameter access for view and guard delegates that receive a <see cref="Core.RouteContext"/>.</summary>
public static class RouteContextCSharpExtensions
{
    /// <summary>Reads a typed parameter from the path first, then from the query.</summary>
    public static bool TryGetParam<T>(this Core.RouteContext context, string name, out T value)
    {
        ArgumentNullException.ThrowIfNull(context);

        return TypedParameters.TryGet(context.UrlMatch.Params, name, out value)
            || TypedParameters.TryGet(context.UrlMatch.QueryParams, name, out value);
    }

    /// <summary>Reads every value of a repeated query parameter.</summary>
    public static IReadOnlyList<T> GetParamValues<T>(this Core.RouteContext context, string name)
    {
        ArgumentNullException.ThrowIfNull(context);

        return TypedParameters.GetMany<T>(context.UrlMatch.QueryParams, name);
    }
}
