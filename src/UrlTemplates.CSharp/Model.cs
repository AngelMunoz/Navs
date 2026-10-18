namespace UrlTemplates.CSharp;

/// <summary>The type declared for a template parameter, such as <c>:id&lt;int&gt;</c>.</summary>
public enum ParameterType
{
    String,
    Int,
    Float,
    Bool,
    Guid,
    Long,
    Decimal
}

/// <summary>Whether a template segment is literal text or a parameter.</summary>
public enum SegmentKind
{
    Plain,
    Parameter
}

/// <summary>One segment of a parsed URL template.</summary>
public sealed class Segment
{
    public SegmentKind Kind { get; }

    /// <summary>The literal text, when <see cref="Kind"/> is <see cref="SegmentKind.Plain"/>.</summary>
    public string? Value { get; }

    /// <summary>The parameter name, when <see cref="Kind"/> is <see cref="SegmentKind.Parameter"/>.</summary>
    public string? Name { get; }

    /// <summary>The declared parameter type, or <c>null</c> for a plain segment.</summary>
    public ParameterType? Type { get; }

    internal Segment(SegmentKind kind, string? value, string? name, ParameterType? type)
    {
        Kind = kind;
        Value = value;
        Name = name;
        Type = type;
    }
}

/// <summary>One query parameter declared by a URL template.</summary>
public sealed class QueryKey
{
    /// <summary>True when the URL must supply this parameter, which the template marks with <c>!</c>.</summary>
    public bool Required { get; }

    public string Name { get; }

    public ParameterType Type { get; }

    internal QueryKey(bool required, string name, ParameterType type)
    {
        Required = required;
        Name = name;
        Type = type;
    }
}

/// <summary>A parsed URL template.</summary>
public sealed class Template
{
    public IReadOnlyList<Segment> Segments { get; }

    public IReadOnlyList<QueryKey> Query { get; }

    /// <summary>The hash fragment, or <c>null</c> when the template declares none.</summary>
    public string? Hash { get; }

    internal CoreTemplate Value { get; }

    internal Template(
        CoreTemplate value,
        IReadOnlyList<Segment> segments,
        IReadOnlyList<QueryKey> query,
        string? hash)
    {
        Value = value;
        Segments = segments;
        Query = query;
        Hash = hash;
    }
}

/// <summary>Whether a parsed query parameter holds one value or several.</summary>
public enum QueryValueKind
{
    Single,
    Multiple
}

/// <summary>A query parameter value read from a URL.</summary>
public sealed class UrlQueryValue
{
    public QueryValueKind Kind { get; }

    /// <summary>The single value, or <c>null</c> when the URL supplied the key without a value.</summary>
    public string? Value { get; }

    /// <summary>The repeated values, when <see cref="Kind"/> is <see cref="QueryValueKind.Multiple"/>.</summary>
    public IReadOnlyList<string> Values { get; }

    internal UrlQueryValue(QueryValueKind kind, string? value, IReadOnlyList<string> values)
    {
        Kind = kind;
        Value = value;
        Values = values;
    }
}

/// <summary>A parsed URL.</summary>
public sealed class Url
{
    public IReadOnlyList<string> Segments { get; }

    public IReadOnlyDictionary<string, UrlQueryValue> Query { get; }

    /// <summary>The hash fragment, or <c>null</c> when the URL has none.</summary>
    public string? Hash { get; }

    internal CoreUrlInfo Value { get; }

    internal Url(
        CoreUrlInfo value,
        IReadOnlyList<string> segments,
        IReadOnlyDictionary<string, UrlQueryValue> query,
        string? hash)
    {
        Value = value;
        Segments = segments;
        Query = query;
        Hash = hash;
    }
}

/// <summary>The parameters extracted by matching a URL against a template.</summary>
public sealed class RouteMatch
{
    public IReadOnlyDictionary<string, object> PathParams { get; }

    public IReadOnlyDictionary<string, object> QueryParams { get; }

    /// <summary>The hash fragment, or <c>null</c> when the URL has none.</summary>
    public string? Hash { get; }

    internal RouteMatch(
        IReadOnlyDictionary<string, object> pathParams,
        IReadOnlyDictionary<string, object> queryParams,
        string? hash)
    {
        PathParams = pathParams;
        QueryParams = queryParams;
        Hash = hash;
    }

    public bool TryGetPathParam<T>(string name, out T value) =>
        Parameters.TryGet(PathParams, name, out value);

    public bool TryGetQueryParam<T>(string name, out T value) =>
        Parameters.TryGet(QueryParams, name, out value);

    /// <summary>Reads a parameter from the path first, then from the query.</summary>
    public bool TryGetParam<T>(string name, out T value) =>
        Parameters.TryGet(PathParams, name, out value)
        || Parameters.TryGet(QueryParams, name, out value);

    /// <summary>Reads every value of a repeated query parameter.</summary>
    public IReadOnlyList<T> GetQueryParamValues<T>(string name) =>
        Parameters.GetMany<T>(QueryParams, name);
}
