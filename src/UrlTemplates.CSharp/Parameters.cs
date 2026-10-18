namespace UrlTemplates.CSharp;

internal static class Parameters
{
    public static bool TryGet<T>(IReadOnlyDictionary<string, object> values, string name, out T value)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(name);

        if (values.TryGetValue(name, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    public static IReadOnlyList<T> GetMany<T>(IReadOnlyDictionary<string, object> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(name);

        if (!values.TryGetValue(name, out var raw))
        {
            return Array.Empty<T>();
        }

        return raw switch
        {
            IEnumerable<object> items => items.OfType<T>().ToArray(),
            T single => new[] { single },
            _ => Array.Empty<T>()
        };
    }
}
