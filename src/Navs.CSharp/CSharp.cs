using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Core = Navs;

namespace Navs.CSharp;

public enum NavigationErrorCode
{
    InvalidTarget,
    RouteNotFound,
    GuardStopped,
    GuardFailed,
    RedirectLoop,
    ActivationFailed,
    HistoryFailed,
    Cancelled,
    Superseded
}

public enum RouterState
{
    Idle,
    Navigating
}

public sealed class NavigationError
{
    public NavigationErrorCode Code { get; }
    public Guid CorrelationId { get; }
    public string Message { get; }

    internal NavigationError(NavigationErrorCode code, Guid correlationId, string message)
    {
        Code = code;
        CorrelationId = correlationId;
        Message = message;
    }
}

public enum GuardDecision
{
    Continue,
    Stop,
    Cancel,
    Redirect
}

public sealed class GuardResult
{
    public GuardDecision Decision { get; }
    public string? RedirectUrl { get; }

    private GuardResult(GuardDecision decision, string? redirectUrl)
    {
        Decision = decision;
        RedirectUrl = redirectUrl;
    }

    public static GuardResult Continue { get; } = new(GuardDecision.Continue, null);
    public static GuardResult Stop { get; } = new(GuardDecision.Stop, null);
    public static GuardResult Cancel { get; } = new(GuardDecision.Cancel, null);

    public static GuardResult Redirect(string url) =>
        new(GuardDecision.Redirect, url);
}

public delegate GuardResult Guard(Core.RouteContext context);

public sealed class RouteSnapshot
{
    public string Path { get; }
    public IReadOnlyDictionary<string, object> Params { get; }
    public IReadOnlyDictionary<string, object> QueryParams { get; }

    internal RouteSnapshot(Core.RouteContext context)
    {
        Path = context.Path;
        Params = context.UrlMatch.Params;
        QueryParams = context.UrlMatch.QueryParams;
    }
}

public sealed class HistoryEntry
{
    public Guid Key { get; }
    public string Url { get; }
    public int Index { get; }

    internal HistoryEntry(Core.HistoryEntry entry)
    {
        Key = entry.Key;
        Url = entry.Url;
        Index = entry.Index;
    }
}

public sealed class HistorySnapshot
{
    public IReadOnlyList<HistoryEntry> Entries { get; }
    public int Index { get; }
    public HistoryEntry? Current { get; }

    internal HistorySnapshot(Core.HistorySnapshot snapshot)
    {
        Entries = snapshot.Entries.Select(entry => new HistoryEntry(entry)).ToArray();
        Index = snapshot.Index;
        var current = snapshot.Current;
        Current = FSharpOption<Core.HistoryEntry>.get_IsSome(current)
            ? new HistoryEntry(current!.Value)
            : null;
    }

    public bool TryGetCurrent(out HistoryEntry? current)
    {
        current = Current;
        return current is not null;
    }
}

public sealed class NavigationResult
{
    public RouteSnapshot Route { get; }
    public HistoryEntry History { get; }
    public IReadOnlyList<string> Redirects { get; }

    internal NavigationResult(Core.NavigationResult result)
    {
        Route = new RouteSnapshot(result.Route);
        History = new HistoryEntry(result.History);
        Redirects = result.Redirects.ToArray();
    }
}

public sealed class NavigationOutcome
{
    public bool IsSuccess { get; }
    public bool IsCancelled { get; }
    public NavigationResult? Result { get; }
    public NavigationError? Error { get; }

    private NavigationOutcome(bool isSuccess, bool isCancelled, NavigationResult? result, NavigationError? error)
    {
        IsSuccess = isSuccess;
        IsCancelled = isCancelled;
        Result = result;
        Error = error;
    }

    internal static NavigationOutcome Success(NavigationResult result) =>
        new(true, false, result, null);

    internal static NavigationOutcome Failure(NavigationError error) =>
        new(false, error.Code == NavigationErrorCode.Cancelled, null, error);
}

public sealed class RouterSnapshot<TView>
{
    public RouterState State { get; }
    public TView? Content { get; }
    public RouteSnapshot? Route { get; }
    public HistorySnapshot History { get; }

    internal RouterSnapshot(Core.RouterSnapshot<TView> snapshot)
    {
        State = snapshot.State.Tag == 0 ? RouterState.Idle : RouterState.Navigating;
        Content = snapshot.Content.IsValueSome ? snapshot.Content.Value : default;
        Route = snapshot.Route.IsValueSome ? new RouteSnapshot(snapshot.Route.Value) : null;
        History = new HistorySnapshot(snapshot.History);
    }
}

public sealed class RouterSnapshotChangedEventArgs<TView> : EventArgs
{
    public RouterSnapshot<TView> Snapshot { get; }

    public RouterSnapshotChangedEventArgs(RouterSnapshot<TView> snapshot)
    {
        Snapshot = snapshot;
    }
}

public interface ICSharpRouter<TView> : IDisposable
{
    Task<NavigationOutcome> NavigateAsync(
        string url,
        CancellationToken cancellationToken = default);

    Task<NavigationOutcome> NavigateAsync(
        string routeName,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken = default);

    Task<NavigationOutcome> BackAsync(CancellationToken cancellationToken = default);
    Task<NavigationOutcome> ForwardAsync(CancellationToken cancellationToken = default);

    RouterSnapshot<TView> Snapshot { get; }
    void ClearCache();
    event EventHandler<RouterSnapshotChangedEventArgs<TView>>? Changed;
}

public sealed class RouteConfigurationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public RouteConfigurationException(IReadOnlyList<string> errors)
        : base("The route table is invalid: " + string.Join("; ", errors))
    {
        Errors = errors;
    }
}

public sealed class RouteDefinition<TView>
{
    internal Core.RouteDefinition<TView> Definition { get; }

    internal RouteDefinition(Core.RouteDefinition<TView> definition)
    {
        Definition = definition;
    }

    public RouteDefinition<TView> CanActivate(Guard guard) =>
        new(Core.Route.CanActivateCSharp(
            context => ToCore(guard(context)),
            Definition));

    public RouteDefinition<TView> CanDeactivate(Guard guard) =>
        new(Core.Route.CanDeactivateCSharp(
            context => ToCore(guard(context)),
            Definition));

    private static Core.GuardResult ToCore(GuardResult result) =>
        result.Decision switch
        {
            GuardDecision.Continue => Core.GuardResult.Continue,
            GuardDecision.Stop => Core.GuardResult.Stop,
            GuardDecision.Cancel => Core.GuardResult.Cancel,
            GuardDecision.Redirect => Core.GuardResult.NewRedirect(result.RedirectUrl ?? "/"),
            _ => Core.GuardResult.Continue
        };

}

public static class Route
{
    public static RouteDefinition<TView> Define<TView>(
        string name,
        string path,
        Func<Core.RouteContext, Core.INavigable, TView> view) =>
        new(Core.Route.Define(name, path, view));

    public static RouteDefinition<TView> Define<TView>(
        string name,
        string path,
        Func<Core.RouteContext, Core.INavigable, CancellationToken, Task<TView>> view) =>
        new(Core.Route.Define(name, path, view));
}

public static class Router
{
    public static ICSharpRouter<TView> Create<TView>(params RouteDefinition<TView>[] routes)
    {
        var result = Navs.Router.Router.CreateDefault(
            routes.Select(route => route.Definition).ToArray());

        if (result.Tag != 0)
        {
            var errors = result.ErrorValue.Select(error => error.ToString()).ToArray();
            throw new RouteConfigurationException(errors);
        }

        return new CSharpRouter<TView>(result.ResultValue);
    }
}

internal sealed class CSharpRouter<TView> : ICSharpRouter<TView>, IDisposable
{
    private readonly Core.IRouter<TView> _router;
    private readonly IDisposable _subscription;

    public CSharpRouter(Core.IRouter<TView> router)
    {
        _router = router;
        _subscription = router.Changed.Subscribe(new SnapshotObserver(this));
    }

    public RouterSnapshot<TView> Snapshot => new(_router.Snapshot);

    public void ClearCache() => _router.ClearCache();

    public event EventHandler<RouterSnapshotChangedEventArgs<TView>>? Changed;

    public async Task<NavigationOutcome> NavigateAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var result = await _router.Navigate(url).Invoke(cancellationToken).ConfigureAwait(false);
        return Map(result);
    }

    public async Task<NavigationOutcome> NavigateAsync(
        string routeName,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _router
            .NavigateByName(routeName, parameters)
            .Invoke(cancellationToken)
            .ConfigureAwait(false);

        return Map(result);
    }

    public async Task<NavigationOutcome> BackAsync(CancellationToken cancellationToken = default)
    {
        var result = await _router.Back().Invoke(cancellationToken).ConfigureAwait(false);
        return Map(result);
    }

    public async Task<NavigationOutcome> ForwardAsync(CancellationToken cancellationToken = default)
    {
        var result = await _router.Forward().Invoke(cancellationToken).ConfigureAwait(false);
        return Map(result);
    }

    public void Dispose() => _subscription.Dispose();

    private static NavigationOutcome Map(FSharpResult<Core.NavigationResult, Core.NavigationError> result)
    {
        if (result.Tag == 0)
        {
            return NavigationOutcome.Success(new NavigationResult(result.ResultValue));
        }

        return NavigationOutcome.Failure(MapError(result.ErrorValue));
    }

    private static NavigationError MapError(Core.NavigationError error)
    {
        var code = error.Tag switch
        {
            0 => NavigationErrorCode.InvalidTarget,
            1 => NavigationErrorCode.RouteNotFound,
            2 => NavigationErrorCode.GuardStopped,
            3 => NavigationErrorCode.GuardFailed,
            4 => NavigationErrorCode.RedirectLoop,
            5 => NavigationErrorCode.ActivationFailed,
            6 => NavigationErrorCode.HistoryFailed,
            7 => NavigationErrorCode.Cancelled,
            8 => NavigationErrorCode.Superseded,
            _ => NavigationErrorCode.ActivationFailed
        };

        return new NavigationError(code, Guid.Empty, error.ToString());
    }

    private sealed class SnapshotObserver : IObserver<Core.RouterSnapshot<TView>>
    {
        private readonly CSharpRouter<TView> _owner;

        public SnapshotObserver(CSharpRouter<TView> owner)
        {
            _owner = owner;
        }

        public void OnNext(Core.RouterSnapshot<TView> value) =>
            _owner.Changed?.Invoke(
                _owner,
                new RouterSnapshotChangedEventArgs<TView>(new RouterSnapshot<TView>(value)));

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }
}
