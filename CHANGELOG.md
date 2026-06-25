# Changelog

## [Unreleased]

### Changed

- **Navs (breaking):** `RouteGuard` is now a two-argument delegate (`RouteContext voption * RouteContext`) and uses `IcedTasks.CancellableValueTask<GuardResponse>`. Update existing guards to accept the previous route (or `ValueNone`) and the target route, and to return a `CancellableValueTask`.
- **Navs (breaking):** Nested routes and `RouteDefinition.children` have been removed. Define all routes at the top level.
- **Navs.Avalonia (breaking):** The view type is now `Control` instead of `#Control`. Existing route handlers returning specific Avalonia control subtypes may need explicit upcasts.
- **Core:** Central package management enabled; all dependencies centralized in `Directory.Packages.props`.
- **Core:** Samples upgraded to `net10.0` and solution files converted to `.slnx`.
- **Navs:** Router refactored for improved modularity, cancellable navigation, and async guard activation/deactivation.
- **Navs.Avalonia:** Updated to Avalonia 12.0.5 and fixed nullability warnings.
- **Navs.FuncUI:** Updated Avalonia/FuncUI dependencies and added logging support.
- **Project:** Fantomas 7.0.2 and `<Nullable>enable` applied across the solution.

### Added

- **Navs.Terminal.Gui:** New adapter library for Terminal.Gui applications.
- **Navs:** Optional `ILogger` support across `Router`, adapters, and `RouterOutlet` for diagnostics during navigation, guards, cache hits, redirects, and route resolution.
- **Navs:** `RouteContext` query-parameter helpers (`getParamSequence`) and an `IDisposableBag` for per-route cleanup.
- **Navs:** `NavigationError<'View>` discriminated union with explicit cases for same-route, cancellation, not found, activation/deactivation failures, and guard redirects.
- **Navs:** Optional `maxRedirectDepth` parameter on `Router.build` (defaults to 5) to cap redirect chain length.
- **UrlTemplates:** Query-parameter parsing improvements and corrected error-message spelling.
- **CI:** Release workflow that extracts the version from this changelog, runs tests, packs, and publishes to NuGet and GitHub Releases.
- **Project:** `AGENTS.md` with agent imperatives and changelog conventions.

### Fixed

- **Navs:** The previous active route's resources are now disposed only after activation guards and content resolution pass, so a failed navigation no longer leaves a broken view on screen.
- **Navs:** Redirect chains are protected against cycles and unbounded depth; the router reports a clear error instead of looping indefinitely.
- **Navs.FuncUI:** Initial-navigation failures now log the underlying exception rather than discarding it.
- **Navs.Terminal.Gui:** The `Text` binding extension now reads/writes `Text` instead of `Title`.
- **UrlTemplates:** Corrected a "Requred" typo in the missing-parameter error message.

## [1.0.0-rc-008] - 2025-06-17

### Changed

- **Navs family:** Last release before central package management and automated changelog-driven releases. No structured changelog was kept prior to this version.
