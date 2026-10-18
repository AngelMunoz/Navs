using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Navs.CSharp;
using Xunit;

public sealed class RouterTests
{
    [Fact]
    public async Task Navigate_succeeds_and_updates_the_snapshot()
    {
        var routes = new[]
        {
            Route.Define<string>("home", "/", (context, navigable) => "Home"),
            Route.Define<string>("user", "/users/:id<int>", (context, navigable) => context.Path)
        };

        var router = Router.Create(routes);

        var outcome = await router.NavigateAsync("/");

        Assert.True(outcome.IsSuccess);
        Assert.Equal("/", outcome.Result!.Route.Path);
        Assert.Equal("Home", router.Snapshot.Content);
        Assert.Equal(RouterState.Idle, router.Snapshot.State);
    }

    [Fact]
    public async Task Missing_route_returns_a_typed_error()
    {
        var router = Router.Create(
            Route.Define<string>("home", "/", (context, navigable) => "Home"));

        var outcome = await router.NavigateAsync("/missing");

        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.Error);
        Assert.Equal(NavigationErrorCode.RouteNotFound, outcome.Error!.Code);
    }

    [Fact]
    public async Task Back_and_forward_move_through_history()
    {
        var routes = new[]
        {
            Route.Define<string>("home", "/", (context, navigable) => "Home"),
            Route.Define<string>("about", "/about", (context, navigable) => "About")
        };

        var router = Router.Create(routes);

        Assert.True((await router.NavigateAsync("/")).IsSuccess);
        Assert.True((await router.NavigateAsync("/about")).IsSuccess);

        var back = await router.BackAsync();

        Assert.True(back.IsSuccess);
        Assert.Equal("Home", router.Snapshot.Content);
        Assert.Equal(0, router.Snapshot.History.Index);

        var forward = await router.ForwardAsync();

        Assert.True(forward.IsSuccess);
        Assert.Equal("About", router.Snapshot.Content);
        Assert.Equal(1, router.Snapshot.History.Index);
    }

    [Fact]
    public async Task Guards_run_in_registration_order_and_can_stop()
    {
        var order = new List<string>();

        var home = Route.Define<string>("home", "/", (context, navigable) => "Home")
            .CanActivate(_ =>
            {
                order.Add("first");
                return GuardResult.Continue;
            })
            .CanActivate(_ =>
            {
                order.Add("second");
                return GuardResult.Continue;
            });

        var about = Route.Define<string>("about", "/about", (context, navigable) => "About")
            .CanActivate(_ => GuardResult.Stop);

        var router = Router.Create(home, about);

        Assert.True((await router.NavigateAsync("/")).IsSuccess);
        Assert.Equal(new[] { "first", "second" }, order);

        var stopped = await router.NavigateAsync("/about");

        Assert.False(stopped.IsSuccess);
        Assert.Equal(NavigationErrorCode.GuardStopped, stopped.Error!.Code);
        Assert.Equal("Home", router.Snapshot.Content);
    }

    [Fact]
    public async Task A_deactivation_guard_can_block_navigation()
    {
        var home = Route.Define<string>("home", "/", (context, navigable) => "Home")
            .CanDeactivate(_ => GuardResult.Stop);

        var about = Route.Define<string>("about", "/about", (context, navigable) => "About");

        var router = Router.Create(home, about);

        Assert.True((await router.NavigateAsync("/")).IsSuccess);

        var stopped = await router.NavigateAsync("/about");

        Assert.False(stopped.IsSuccess);
        Assert.Equal(NavigationErrorCode.GuardStopped, stopped.Error!.Code);
        Assert.Equal("Home", router.Snapshot.Content);
    }

    [Fact]
    public async Task A_cancelled_navigation_returns_a_cancelled_outcome()
    {
        var router = Router.Create(
            Route.Define<string>("home", "/", (context, navigable) => "Home"));

        using var source = new CancellationTokenSource();
        source.Cancel();

        var outcome = await router.NavigateAsync("/", source.Token);

        Assert.False(outcome.IsSuccess);
        Assert.True(outcome.IsCancelled);
        Assert.NotNull(outcome.Error);
        Assert.Equal(NavigationErrorCode.Cancelled, outcome.Error!.Code);
    }

    [Fact]
    public async Task History_snapshot_exposes_the_current_entry()
    {
        var router = Router.Create(
            Route.Define<string>("home", "/", (context, navigable) => "Home"));

        Assert.True((await router.NavigateAsync("/")).IsSuccess);

        var history = router.Snapshot.History;

        Assert.Single(history.Entries);
        Assert.True(history.TryGetCurrent(out var current));
        Assert.NotNull(current);
        Assert.Equal("/", current!.Url);
    }

    [Fact]
    public void Invalid_configuration_throws_a_configuration_exception()
    {
        var routes = new[]
        {
            Route.Define<string>("home", "/", (context, navigable) => "Home"),
            Route.Define<string>("home", "/about", (context, navigable) => "About")
        };

        Assert.Throws<RouteConfigurationException>(() => Router.Create(routes));
    }

    [Fact]
    public void Public_surface_does_not_expose_fsharp_types()
    {
        AssertNoFSharpTypes(typeof(ICSharpRouter<string>));
        AssertNoFSharpTypes(typeof(NavigationOutcome));
        AssertNoFSharpTypes(typeof(RouteSnapshot));
        AssertNoFSharpTypes(typeof(HistorySnapshot));
        AssertNoFSharpTypes(typeof(Route));
        AssertNoFSharpTypes(typeof(Router));
    }

    private static void AssertNoFSharpTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var method in type.GetMethods(flags))
        {
            AssertClean(method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                AssertClean(parameter.ParameterType);
            }
        }

        foreach (var property in type.GetProperties(flags))
        {
            AssertClean(property.PropertyType);
        }
    }

    private static void AssertClean(Type type)
    {
        var name = type.FullName ?? type.Name;

        Assert.DoesNotContain("FSharp", name);
        Assert.DoesNotContain("ValueOption", name);
        Assert.DoesNotContain("CancellableTask", name);
        Assert.DoesNotContain("FSharpResult", name);
    }
}
