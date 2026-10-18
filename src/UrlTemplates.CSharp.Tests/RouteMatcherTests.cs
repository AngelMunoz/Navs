using System.Reflection;
using UrlTemplates.CSharp;
using Xunit;

public sealed class RouteMatcherTests
{
    [Fact]
    public void Parses_a_template_into_segments_and_query_keys()
    {
        var parsed = UrlTemplate.TryParse(
            "/api/v1/users/:userId/posts/:postId<int>?name&age<int>!",
            out var template,
            out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.NotNull(template);

        Assert.Equal(
            new[] { "", "api", "v1", "users", null, "posts", null },
            template!.Segments.Select(segment => segment.Value));

        var userId = template.Segments[4];
        Assert.Equal(SegmentKind.Parameter, userId.Kind);
        Assert.Equal("userId", userId.Name);
        Assert.Equal(ParameterType.String, userId.Type);

        var postId = template.Segments[6];
        Assert.Equal(SegmentKind.Parameter, postId.Kind);
        Assert.Equal("postId", postId.Name);
        Assert.Equal(ParameterType.Int, postId.Type);

        Assert.Equal(2, template.Query.Count);
        Assert.False(template.Query[0].Required);
        Assert.Equal("name", template.Query[0].Name);
        Assert.Equal(ParameterType.String, template.Query[0].Type);
        Assert.True(template.Query[1].Required);
        Assert.Equal("age", template.Query[1].Name);
        Assert.Equal(ParameterType.Int, template.Query[1].Type);
    }

    [Fact]
    public void Reports_the_offending_character_for_an_invalid_template()
    {
        var parsed = UrlTemplate.TryParse("/api/:user-name", out var template, out var error);

        Assert.False(parsed);
        Assert.Null(template);
        Assert.Equal("unexpected '-' at index 10", error);
    }

    [Fact]
    public void Matches_a_url_and_reads_typed_path_parameters()
    {
        var matched = RouteMatcher.TryMatch(
            "/api/v1/users/:userId/posts/:postId<int>",
            "/api/v1/users/123/posts/456",
            out var match,
            out var errors);

        Assert.True(matched);
        Assert.Empty(errors);
        Assert.NotNull(match);

        Assert.True(match!.TryGetPathParam<string>("userId", out var userId));
        Assert.Equal("123", userId);

        Assert.True(match.TryGetPathParam<int>("postId", out var postId));
        Assert.Equal(456, postId);

        Assert.False(match.TryGetPathParam<int>("userId", out _));
        Assert.False(match.TryGetPathParam<string>("missing", out _));
    }

    [Fact]
    public void Reads_query_parameters_from_the_match()
    {
        var matched = RouteMatcher.TryMatch(
            "/api?name&age<int>",
            "/api?name=john&age=30",
            out var match,
            out var errors);

        Assert.True(matched);
        Assert.Empty(errors);

        Assert.True(match!.TryGetQueryParam<string>("name", out var name));
        Assert.Equal("john", name);

        Assert.True(match.TryGetQueryParam<int>("age", out var age));
        Assert.Equal(30, age);

        Assert.True(match.TryGetParam<int>("age", out var viaBoth));
        Assert.Equal(30, viaBoth);
    }

    [Fact]
    public void Reports_a_literal_segment_that_does_not_match()
    {
        var matched = RouteMatcher.TryMatch("/hello/world", "/hello/earth", out var match, out var errors);

        Assert.False(matched);
        Assert.Null(match);

        var error = Assert.Single(errors);
        Assert.Equal(MatchErrorKind.SegmentMismatch, error.Kind);
        Assert.Equal("world", error.TemplateSegment);
        Assert.Equal("earth", error.UrlSegment);
    }

    [Fact]
    public void Reports_missing_required_query_parameters()
    {
        var matched = UrlTemplates.CSharp.RouteMatcher.TryMatch(
            "/api?name!&age!",
            "/api?name=john",
            out _,
            out var errors);

        Assert.False(matched);

        var error = Assert.Single(errors);
        Assert.Equal(MatchErrorKind.MissingQueryParams, error.Kind);
        Assert.Equal(new[] { "age" }, error.Missing);
    }

    [Fact]
    public void Reports_a_path_parameter_that_does_not_parse()
    {
        var matched = UrlTemplates.CSharp.RouteMatcher.TryMatch(
            "/users/:id<int>",
            "/users/abc",
            out _,
            out var errors);

        Assert.False(matched);

        var error = Assert.Single(errors);
        Assert.Equal(MatchErrorKind.UnparsableParam, error.Kind);
        Assert.Equal("id", error.Name);
        Assert.Equal(ParameterType.Int, error.Expected);
        Assert.Equal("abc", error.Value);
    }

    [Fact]
    public void Reports_a_required_query_parameter_that_does_not_parse()
    {
        var matched = UrlTemplates.CSharp.RouteMatcher.TryMatch(
            "/api?age<int>!",
            "/api?age=abc",
            out _,
            out var errors);

        Assert.False(matched);

        var error = Assert.Single(errors);
        Assert.Equal(MatchErrorKind.QueryParameterUnparsable, error.Kind);
        Assert.Equal("age", error.Name);
        Assert.Equal(ParameterType.Int, error.Expected);
        Assert.Equal("abc", error.Value);
    }

    [Fact]
    public void Reports_repeated_query_values_that_do_not_parse()
    {
        var matched = UrlTemplates.CSharp.RouteMatcher.TryMatch(
            "/api?statuses<int>!",
            "/api?statuses=1&statuses=x",
            out _,
            out var errors);

        Assert.False(matched);

        var error = Assert.Single(errors);
        Assert.Equal(MatchErrorKind.QueryParametersUnparsable, error.Kind);

        var item = Assert.Single(error.Unparsable);
        Assert.Equal("statuses", item.Name);
        Assert.Equal(ParameterType.Int, item.Expected);
        Assert.Equal("x", item.Value);
    }

    [Fact]
    public void Parses_a_url_with_query_and_hash()
    {
        Assert.True(
            UrlTemplates.CSharp.RouteMatcher.TryParseUrl(
                "/api?name=john&status=a&status=b#top",
                out var url,
                out var error));

        Assert.Null(error);
        Assert.NotNull(url);
        Assert.Equal(new[] { "", "api" }, url!.Segments);
        Assert.Equal("top", url.Hash);

        var name = url.Query["name"];
        Assert.Equal(QueryValueKind.Single, name.Kind);
        Assert.Equal("john", name.Value);

        var statuses = url.Query["status"];
        Assert.Equal(QueryValueKind.Multiple, statuses.Kind);
        Assert.Equal(new[] { "b", "a" }, statuses.Values);
    }

    [Fact]
    public void Builds_a_url_from_a_template()
    {
        var parameters = new Dictionary<string, object> { ["id"] = 42 };

        Assert.True(UrlTemplate.TryBuild("/users/:id<int>", parameters, out var url, out var errors));

        Assert.Equal("/users/42", url);
        Assert.Empty(errors);
    }

    [Fact]
    public void Reports_a_missing_parameter_when_building_a_url()
    {
        Assert.False(UrlTemplate.TryBuild("/users/:id<int>", null, out var url, out var errors));

        Assert.Null(url);
        Assert.Contains(errors, message => message.Contains("id", StringComparison.Ordinal));
    }

    [Fact]
    public void Matching_accepts_an_already_parsed_template_and_url()
    {
        Assert.True(UrlTemplate.TryParse("/users/:id<int>", out var template, out _));
        Assert.True(UrlTemplates.CSharp.RouteMatcher.TryParseUrl("/users/42", out var url, out _));

        Assert.True(
            UrlTemplates.CSharp.RouteMatcher.TryMatch(template!, url!, out var match, out var errors));

        Assert.Empty(errors);
        Assert.True(match!.TryGetPathParam<int>("id", out var id));
        Assert.Equal(42, id);
    }

    [Fact]
    public void Public_surface_does_not_expose_fsharp_types()
    {
        AssertClean(typeof(Template));
        AssertClean(typeof(Url));
        AssertClean(typeof(RouteMatch));
        AssertClean(typeof(MatchError));
        AssertClean(typeof(Segment));
        AssertClean(typeof(QueryKey));
        AssertClean(typeof(UrlQueryValue));
        AssertClean(typeof(UnparsableItem));
        AssertClean(typeof(UrlTemplates.CSharp.UrlTemplate));
        AssertClean(typeof(UrlTemplates.CSharp.RouteMatcher));
    }

    [Fact]
    public void Parameter_type_enum_covers_every_core_case()
    {
        var coreCases = typeof(UrlTemplates.UrlTemplate.TypedParam)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(property =>
                property.PropertyType == typeof(bool)
                && property.Name.StartsWith("Is", StringComparison.Ordinal));

        Assert.Equal(coreCases, Enum.GetValues<ParameterType>().Length);
    }

    private static void AssertClean(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(flags))
        {
            Check(method.ReturnType);

            foreach (var parameter in method.GetParameters())
            {
                var parameterType = parameter.ParameterType;
                Check(parameterType.IsByRef ? parameterType.GetElementType()! : parameterType);
            }
        }

        foreach (var property in type.GetProperties(flags))
        {
            Check(property.PropertyType);
        }

        static void Check(Type subject)
        {
            var name = subject.FullName ?? subject.Name;

            Assert.DoesNotContain("FSharp", name);
            Assert.DoesNotContain("ValueOption", name);
            Assert.DoesNotContain("CancellableTask", name);
            Assert.DoesNotContain("FSharpResult", name);

            if (subject.IsGenericType)
            {
                foreach (var argument in subject.GetGenericArguments())
                {
                    Check(argument);
                }
            }
        }
    }
}
