# Navs Router v2 — Implementation Plan

Status: implementation complete  
Date: 2026-10-17  
Repository: E:\Navs  
Scope: phases 0–8, public API break allowed

This document is the tracking source for the router restructuring. Update the checkboxes and the status table as work lands. Do not mark a phase complete until its exit criteria pass.

## 0. Current progress (2026-10-17)

- New core contracts and DSL are implemented in `src/Navs/Types.fs`.
- Route table compilation, ranking, matching, `InMemoryHistory`, the navigation coordinator and the basic cache are implemented in `src/Navs/Router.fs`.
- The F# facade returns `CancellableTask<Result<...>>`; cancellation is mapped to `Error Cancelled`.
- Guard order is registration order in the new F# DSL.
- The new test suite in `src/Navs.Tests/V2.fs` passes: 27 tests on net8.0 and 27 on net10.0.
- `dotnet build Navs.slnx` now succeeds; the full test run passes: 27 core tests, 9 C# tests, 5 adapter tests, 1 FuncUI test and 33 UrlTemplates tests.
- The old `.fsi`, `DSL.fs`, `DSL.Interop.fs` and old test files were removed.
- The three adapters were reduced to the new `IRouter` shape and compile. Their headless E2E suites and the shared harness are still pending.
- The C# facade exists in `src/Navs.CSharp`. A temporary C# smoke test navigated, read the snapshot and went back successfully.
- New `Types.fsi` and `Router.fsi` files were generated and corrected. Representation-only work types remain internal; the public-surface test verifies they are not exported.
- The F# suite now includes a reflection test that rejects `ValueTask` and internal work types in the public surface.
- `src/Navs.CSharp.Tests` was added with 9 xUnit tests: navigation, missing route, history, cancellation, guard order, deactivation guards, configuration errors and a reflection check that rejects F# types in the C# surface.
- `src/Navs.Adapters.Tests` was added with 5 tests: Avalonia rendering, splash/no-content fallback, subscription replacement, Terminal.Gui router/outlet replacement and Avalonia headless startup.
- `src/Navs.FuncUI.Tests` was added with 1 test for the FuncUI router and content snapshot.
- The shared headless E2E harness for Avalonia, FuncUI and Terminal.Gui is still pending.

### F# skill audit snapshot

- `dotnet fantomas --check src` is clean.
- No `Option.get`, `ValueOption.get`, `List.head` or `Seq.head` appears in `src`.
- `ValueTask` appears only in the internal sync work types; the public-surface test passes.
- The guard loop has an empty fast path and reuses one mutable cursor over the guard list.
- `RouteTable.Compile` was refactored to validate with immutable results; the local mutable flag is gone.
- `ConcurrencyPolicy.Serialize` and `Reject` are implemented and covered by deterministic tests.
- URL building uses the compiled `UrlTemplate`, encodes segment/query values and omits missing optional query entries. URL parsing percent-decodes path, query and hash values, so generated URLs round-trip. Table tests cover unicode, spaces, repeated query, `+` decoding and hash; an FsCheck property covers arbitrary path values.
- Cache entries now carry a timestamp; TTL expiry and bounded eviction are implemented and tested.
- `README.md` was rewritten for the v2 F#/C# API.
- `docs/Navs.fsx` was rewritten around `Route.define`, `Router.createDefault`, `Snapshot`, guards, history and cache.
- The Avalonia, FuncUI and Terminal.Gui docs and the adapter guide were rewritten for the v2 API.
- The `Routerish` Avalonia sample was migrated to the v2 API and builds cleanly.
- The `FuncUI` sample was migrated to the v2 API and builds cleanly.
- The `TGUI` sample was migrated to the v2 API and builds cleanly.
- The `NxuiDeclarative` and `CSharpToo` samples were migrated; `NxuiDeclarative` builds and `CSharpToo` runs the C# facade end to end.
- Route disposal failures are logged through the router's `ILogger` instead of being swallowed.
- Leaving a `NoCache` route is covered by a disposal test.
- `RouteDisposables` moved from `Types.fsi` into `Router.fs`; it no longer appears in the public signature.
- `ICSharpRouter<TView>` now implements `IDisposable`, so C# consumers can release the changed-event subscription.
- `IRouter.ClearCache` clears and disposes cached views; adapters and the C# facade delegate to it.
- C# routes expose `CanActivate` and `CanDeactivate` with the C#-friendly `GuardResult`/`Guard` API.
- The Avalonia outlet removes its previous router subscription; the FuncUI `useRouter` effect returns an `IDisposable` subscription; the Terminal.Gui outlet removes it on `Disposing`.
- Avalonia splash and no-content fallback are covered by adapter tests.
- `Avalonia.Headless` is referenced by the adapter test project; the headless platform startup test passes.
- Guard-blocked back navigation, same-URL `Ignore` and same-URL `Push` are covered by core tests.
- Route table validation rejects ambiguous parameter patterns that produce the same URL shape.
- Declarative redirects resolve through the target chain and replace history; guard redirects and redirect cycles are covered by tests.
- A superseded navigation no longer commits history before discovering that it lost the race.
- `CHANGELOG.md` now has the v2 Added/Changed/Fixed entries.
- Final source review: no `Option.get`/`ValueOption.get`/`List.head`/`Seq.head`; mutable state is limited to the coordinator, in-memory history and UI outlets; `invalidOp` remains only for startup route configuration; all five samples build.
- Performance pass: single URL resolution, arrays + `FrozenDictionary` for the route table, cached history/router snapshots, precomputed global guards, lazy route disposables and a pooled URL buffer.
- Option/Result helper pass: dictionary access goes through `Dictionary.tryGetValue`; nested matches in URL building, route lookup, cache lookup, history navigation, adapters and tests now use `ValueOption`/`Option`/`Result` combinators or extracted helpers (`UrlBuilder.tryParameter`, `InMemoryHistory.tryAt`, `tryCachedView`, `cacheView`, `RouteFactory`). No helper is declared in a `.fsi`, so none is public.
- Hybrid async pass: `Views.run` and `Guards.runOne` keep the token explicit and return `ValueTask`; `Guards.run`, `navigateOnce`, `NavigateCore` and `NavigateWithPolicy` use `cancellableValueTask`. Guard redirects run in a `while` loop with a redirect counter, so no `Task` recursion and no `List.length` per step. A 10,000-redirect guard test verifies no stack overflow.
- Helper modules keep `RouterImpl` small: `Redirects`, `Views`, `Guards`, `ViewCache`, `HistoryNavigation`, `RouteLifetime`, `RouteSignature` and `RouteValidation`. Small helpers use `let inline`; `ViewCache.store` takes an `[<InlineIfLambda>]` predicate; `RouteLinks` already uses `[<InlineIfLambda>]`.
- `Router.fsi` now declares only `InMemoryHistory` and `Router`; internal helper types are hidden by the signature. `Types.fsi` keeps the internal work types that `Router.fs` needs for the `RouteDefinition` representation.
- Multi-value results use records (`RedirectResolution`, `ActivatedNavigation`) instead of 4-tuples. `RouteSignature.ofTemplate` uses `List.mapFold` instead of a mutable score. `match!` replaces the remaining `let!` plus `match` pairs.
- `CHANGELOG.md` covers the v2 added/changed/fixed behavior under `[Unreleased]`; `AGENTS.md` lists the new `Navs.CSharp` package, the test projects and the `CSharpToo` sample.
- Deferred follow-ups: full headless outlet/`useRouter` interaction tests; `invalidOp` remains the deliberate startup failure for adapter route configuration.


## 1. Dry-run result

The architecture is viable. The core risks were checked with temporary F# and C# projects outside the repository. No repository source was changed for the dry run.

| Check | Result | Consequence |
| --- | --- | --- |
| Current solution builds | 0 errors; 6 NU1608 warnings (Expecto test SDK) | Baseline is clean enough to start |
| Current tests | 32 passed on net8.0 and 32 on net10.0 | Baseline behavior is covered |
| F# router returns `CancellableTask<Result<...>>` | Compiles and runs | Accepted as the F# public shape |
| Cancellation in `CancellableTask` | The builder throws `TaskCanceledException` on a pre-cancelled token | The router boundary must catch cancellation and map it to `Error Cancelled` |
| `cancellableTask` route handler | Compiles through the existing `Task` handler overload | No separate `CancellableTask` overload is needed |
| Adding a separate `CancellableTask` overload | Duplicate overload FS0041 | Do not add it |
| F# `Async` and `Task` route registration | Requires explicit `Route.define<'View>` annotation | Keep the current style and test it; document it |
| F# guard order | Two guards registered first/second execute second/first | Fix F# to append; C# already appends |
| C# consumes F# `CancellableTask` | `.Invoke(token)` works; `FSharpResult.Tag` and `ResultValue` are accessible | A thin C# facade can map to `NavigationOutcome` |
| Avalonia headless package | 12.0.5 and 11.1.0 exist on NuGet | Adapter E2E is feasible |
| Terminal.Gui testing support | 2.4.11 has `InputInjector`, `FakeClipboard` and testable input | Adapter E2E is feasible; validate the exact harness in phase 8 |

Residual risks:

- Avalonia and Terminal.Gui headless harnesses were not fully built during the dry run.
- The C# mapping from F# `Result` to `NavigationOutcome` needs a small, tested bridge; it must not expose `FSharpResult` publicly.
- Browser history `popstate` coordination is the hardest history case and belongs to phase 4 plus a future browser adapter.
- `Async` and `Task` route registration requires a type annotation today. If that remains unacceptable, add explicitly named registration functions in phase 6 instead of relying on overload resolution.

## 2. Locked decisions

- F# navigation returns `CancellableTask<Result<NavigationResult, NavigationError>>`.
- C# navigation returns `Task<NavigationOutcome>` with an explicit `CancellationToken`.
- No `ValueTask` appears in a public signature.
- No `CancellableTask` appears in the C# surface.
- F# route registration stays on `Route.define`. There is no `Route.viewTask` or similar builder.
- `cancellableTask` handlers use the existing `Task` handler overload, which is type-compatible with `CancellableTask`.
- Do not add a separate `CancellableTask` overload; it duplicates the `Task` overload.
- F# guard order is registration order. Fix `Route.canActivate` and `Route.canDeactivate` to append. C# already appends.
- `RouteDefinition` becomes opaque. Users register through the DSL; `Router.create` validates and compiles.
- The navigation coordinator is the only writer of the router state and snapshot.
- `ValueTask` is internal and only represents the synchronous route case.
- Async route work uses `CancellableTask` internally.
- Default cache policy is `NoCache`.
- Default same-URL policy is `Ignore`.
- Default concurrency policy is `LatestWins`.
- Initial navigation uses `Replace`; redirects use `Replace`; normal navigation uses `Push`.
- C# configuration errors throw `RouteConfigurationException` at startup. Navigation failures and cancellation return `NavigationOutcome`.
- All packages move to 2.0. No compatibility shim over the old router.

## 3. Target components

| Component | Kind | Responsibility |
| --- | --- | --- |
| `IRouter<'View>` | Public interface | Facade for snapshot, navigation, history and links |
| `INavigable` | Public interface | Navigation from route handlers |
| `ICSharpRouter<'View>` | Public interface | C# facade with `Task<NavigationOutcome>` |
| `IHistory` | Public interface | Read-only history snapshot |
| `IHistoryStore` | Public interface | Platform write port for history |
| `IRouteLinks` | Public interface | Build URLs and query active state |
| `IViewCache<'View>` | Public interface | Cache policy and lifecycle |
| `INavigationObserver` | Optional public interface | Navigation events for UI and diagnostics |
| `IRouteMatcher<'View>` | Internal interface | Test seam for the coordinator |
| `IViewFactory<'View>` | Internal interface | Test seam for activation |
| `Router<'View>` | Internal concrete | Implements `IRouter` |
| `RouteTable<'View>` | Internal concrete | Compiled routes, matching, ranking, URL building |
| `NavigationCoordinator` | Internal concrete | Sessions, state, concurrency, commit/abort |
| `NavigationSession` | Internal concrete | One navigation attempt |
| `GuardPipeline` | Internal concrete | Global and per-route guards |
| `ResolverPipeline` | Internal concrete | Route data loading |
| `ViewActivator` | Internal concrete | Resolve or create the view |
| `InMemoryHistory` | Public concrete | Default `IHistoryStore` |
| `RouteLinks` | Internal concrete | Implements `IRouteLinks` |
| `LruViewCache<'View>` | Public concrete | Bounded cache implementation |

## 4. Final surfaces

### F# navigation

```fsharp
type INavigable =
    abstract Navigate:
        url: string ->
        CancellableTask<Result<NavigationResult, NavigationError>>

    abstract NavigateByName:
        name: string * parameters: RouteParams ->
        CancellableTask<Result<NavigationResult, NavigationError>>

    abstract Back:
        unit -> CancellableTask<Result<NavigationResult, NavigationError>>

    abstract Forward:
        unit -> CancellableTask<Result<NavigationResult, NavigationError>>
```

The coordinator catches `OperationCanceledException` from the `CancellableTask` boundary and returns `Error Cancelled`. Cancellation must not escape as an exception.

### F# route registration

```fsharp
let routes = [
    Route.define<Page>("home", "/", fun _ _ -> home)

    Route.define<Page>("user", "/users/:id<int>", fun ctx _ -> async {
        let! user = loadUser ctx
        return userPage user
    })

    Route.define<Page>("user", "/users/:id<int>", fun ctx _ token -> task {
        let! user = loadUser ctx token
        return userPage user
    })

    Route.define<Page>("user", "/users/:id<int>", fun ctx _ -> cancellableTask {
        let! token = CancellableTask.getCancellationToken()
        let! user = loadUser ctx token
        return userPage user
    })
]
```

Notes:

- `Async` and `Task` registration needs `Route.define<Page>` today. Tests must cover this.
- The `cancellableTask` example uses the `Task` overload through the type abbreviation. No extra overload.
- No handler executes at registration. `ViewWork` is produced when the route is activated.

### Internal work types

```fsharp
type internal ViewWork<'View> =
    | Sync of (RouteContext -> INavigable -> ValueTask<'View>)
    | Async of (RouteContext -> INavigable -> CancellableTask<'View>)

type internal GuardWork =
    | Sync of (RouteContext voption -> RouteContext -> ValueTask<GuardResult>)
    | Async of (RouteContext voption -> RouteContext -> CancellableTask<GuardResult>)
```

The sync case uses `ValueTask`. The async case uses `CancellableTask`. The pipeline invokes the stored function with the route context and token at activation time.

### C# surface

```csharp
Task<NavigationOutcome> NavigateAsync(
    string url,
    CancellationToken cancellationToken = default);

Task<NavigationOutcome> NavigateAsync(
    string routeName,
    IReadOnlyDictionary<string, object> parameters,
    CancellationToken cancellationToken = default);

Task<NavigationOutcome> BackAsync(CancellationToken cancellationToken = default);
Task<NavigationOutcome> ForwardAsync(CancellationToken cancellationToken = default);
```

`NavigationOutcome` exposes `IsSuccess`, `IsCancelled`, `Result` and `Error`. `NavigationErrorCode` is a C# enum. No F# `Result`, `Option`, `ValueOption`, `unit`, `FSharpFunc`, `ValueTask` or `CancellableTask` appears in the C# public surface.

## 5. Phase plan

### Phase 0 — Contracts, DSL and decisions

- [ ] Write the architecture decision record for the locked decisions in section 2.
- [ ] Define `Contracts.fsi` with the F# public types and interfaces.
- [ ] Define the C# public surface in a design document or skeleton project.
- [ ] Define `ViewWork` and `GuardWork` as internal types.
- [ ] Fix F# guard order: append in `Route.canActivate` and `Route.canDeactivate`.
- [ ] Add a compile-time matrix for F# sync, `Async`, `Task` and `cancellableTask`.
- [ ] Add a compile-time matrix for C# sync and `Task` with token.
- [ ] Add a test that two F# guards execute in registration order.
- [ ] Add a test that two C# guards execute in registration order.
- [ ] Confirm that no public signature contains `ValueTask`.
- [ ] Confirm that the F# router can catch `OperationCanceledException` and return `Error Cancelled`.

Exit criteria: all compile matrices pass; guard order is registration order in both surfaces; the public surface test rejects `ValueTask`.

### Phase 1 — URL and route table

- [ ] Introduce `Url` with `parse` and `format`.
- [ ] Introduce `RoutePattern` backed by the existing `UrlTemplate` parser.
- [ ] Parse patterns once in `Router.create`, not on each navigation.
- [ ] Add `RouteTable.create` with validation:
  - [ ] empty table
  - [ ] duplicate names
  - [ ] invalid patterns
  - [ ] ambiguous patterns
  - [ ] duplicate static paths
  - [ ] invalid redirects
- [ ] Add deterministic ranking: static, typed parameter, untyped parameter, wildcard, explicit priority.
- [ ] Add `RouteTable.match` and `RouteTable.tryBuildUrl`.
- [ ] Encode URL parameter values and query values.
- [ ] Add property tests for URL round-trip.
- [ ] Add tests for optional query, repeated query, trailing slash, unicode and hash.

Exit criteria: invalid route tables fail at `Router.create`; ranking is deterministic; URL round-trip passes.

### Phase 2 — Navigation coordinator and state

- [ ] Introduce `NavigationSession` with id, token, phase and completion state.
- [ ] Introduce `NavigationCoordinator` as the only state writer.
- [ ] Add `RouterSnapshot` and `Changed`.
- [ ] Add `ConcurrencyPolicy = LatestWins | Serialize | Reject`.
- [ ] Map cancellation from `CancellableTask` to `Error Cancelled`.
- [ ] Ensure every exit path clears the active session:
  - [ ] success
  - [ ] route error
  - [ ] guard stop
  - [ ] exception
  - [ ] cancellation
  - [ ] superseded
- [ ] Add tests for pre-cancelled token and cancellation at each stage.
- [ ] Add tests for two concurrent navigations and nested navigation.
- [ ] Add a state-sequence test that verifies `Idle` after every completion.

Exit criteria: no path leaves `Navigating`; late sessions cannot overwrite a newer state; concurrency policies behave as documented.

### Phase 3 — Guards and redirects

- [ ] Introduce `GuardResult = Continue | Stop | Redirect of NavigationTarget | Cancel`.
- [ ] Run global guards, then deactivation guards, then activation guards.
- [ ] Preserve registration order in both F# and C#.
- [ ] Reject or restart on redirect according to one documented policy.
- [ ] Bound redirect chains and cycles with a typed error.
- [ ] Use `Replace` for redirect history entries.
- [ ] Test `Stop`, `Redirect`, guard exception and cancellation at each phase.
- [ ] Test a guard on a cached route.
- [ ] Test a guard blocking back/forward.

Exit criteria: guards cannot leave state or history inconsistent; redirect loops return a typed error; order tests pass.

### Phase 4 — History

- [ ] Define `IHistory` as read-only and `IHistoryStore` as the write port.
- [ ] Implement `InMemoryHistory`.
- [ ] Add entries with `Key`, `Url`, `Index` and opaque `State`.
- [ ] Implement push, replace, go, truncation and bounds.
- [ ] Implement a history transaction: peek target, run navigation, commit or rollback.
- [ ] Suppress the router's own history events by `HistoryKey`.
- [ ] Convert external history changes into navigation requests.
- [ ] Add tests for initial replace, normal push, redirect replace, back/forward, guard block rollback and same-URL policy.

Exit criteria: history is consistent after success, failure and cancellation; back/forward never bypass guards.

### Phase 5 — Views, resolvers and cache

- [ ] Implement `ViewWork` activation without executing handlers at registration.
- [ ] Add optional `ResolverPipeline`.
- [ ] Add `IViewCache` and `LruViewCache`.
- [ ] Add `NoCache` as default and `Cache` as opt-in.
- [ ] Dispose cached views on eviction and invalidation.
- [ ] Run guards on cache hits; skip resolvers and view creation on hits.
- [ ] Add tests for hit, miss, hit with guard, eviction, invalidation and `NoCache` disposal.

Exit criteria: cache has a bounded policy; disposed views are not reused; cache hits still enforce guards.

### Phase 6 — F# facade and DSL

- [ ] Implement `INavigable` and `IRouter<'View>` with `CancellableTask`.
- [ ] Implement `Router.create` returning `Result<IRouter<'View>, RouteTableError list>`.
- [ ] Keep `Route.define` as the registration surface.
- [ ] Keep the sync, `Async` and `Task` overloads.
- [ ] Verify `cancellableTask` compiles through the `Task` overload; do not add a duplicate.
- [ ] Make `RouteDefinition` opaque in the `.fsi`.
- [ ] Add `Route.canActivate` / `canDeactivate` in registration order.
- [ ] Remove `ActiveRouteParams` and `SameRouteNavigation`.
- [ ] Replace same-route handling with `SameUrlPolicy`.
- [ ] Add public-surface tests for F#.

Exit criteria: F# core E2E passes; no `ValueTask` in public signatures; `Route.define` matrices pass.

### Phase 7 — C# facade

- [ ] Create the C# facade project.
- [ ] Add `ICSharpRouter<'View>`, `NavigationOutcome`, `NavigationErrorCode` and `NavigationError`.
- [ ] Add C# route registration for sync and `Task` with token.
- [ ] Add `Router.Create` and optional `Router.TryCreate`.
- [ ] Map F# `Result` and `CancellableTask` to `Task<NavigationOutcome>`.
- [ ] Map configuration errors to `RouteConfigurationException`.
- [ ] Map cancellation to `IsCancelled`.
- [ ] Add a C# consumer test project.
- [ ] Add a reflection test that rejects F# types in the C# public surface.

Exit criteria: C# consumer compiles and runs; no F# `Result`, `Option`, `ValueOption`, `unit`, `FSharpFunc`, `ValueTask` or `CancellableTask` is public.

### Phase 8 — Adapters

- [ ] Change `Navs.Avalonia.Routes` to accept `RouteDefinition<Control>` directly.
- [ ] Remove the adapter-specific `Route` wrapper types.
- [ ] Keep `RouterOutlet` as a render-only control.
- [ ] Update `Navs.FuncUI` and `Navs.Terminal.Gui` to use the core `RouteDefinition`.
- [ ] Build a shared `IAdapterHarness<'View>` for tests.
- [ ] Add Avalonia headless tests for splash, content, no content, transition and disposal.
- [ ] Add FuncUI headless tests for the outlet and `useRouter`.
- [ ] Add Terminal.Gui tests with the test input/driver support.
- [ ] Update samples and docs to the new API.

Exit criteria: the shared adapter suite passes; no adapter contains routing rules; docs and samples compile.

## 6. Breaking changes

| Class | Expected change |
| --- | --- |
| F# source | `Navigate` returns `CancellableTask`; callers pass the token when invoking; `RouteDefinition` becomes opaque; `GetView` and `RouteGuard` change; `State`/`Content` move to `Snapshot`/`Changed` |
| C# source | New `Navs.CSharp` facade; `Result` removed; `ICSharpRouter` replaces the F#-shaped router; route and guard delegates change |
| Binary | All packages 2.0; no type forwarding; recompile required |
| Behavior | Guard order corrected; `NoCache` default; route ranking; same URL ignored; redirects replace history; `LatestWins` |
| Data/URL | Parameter and query encoding; typed URL generation; new history state and error codes |
| Adapters/docs | Core `RouteDefinition` in adapters; new outlet API; README, docs and samples rewritten |

## 7. Test plan

### E2E levels

| Level | Setup | Tool |
| --- | --- | --- |
| DSL compile | F# sync/Async/Task/cancellableTask; C# sync/Task | Compiler tests |
| Core E2E | Real router, table, history, cache and `string` view | Expecto |
| Avalonia E2E | Real `Window`, `RouterOutlet` and controls | Avalonia.Headless |
| FuncUI E2E | Real `Component` and `RouterOutlet` | Avalonia.Headless |
| Terminal.Gui E2E | Real `Window` and `RouterOutlet` | Terminal.Gui test input |
| C# E2E | C# consumer of `Navs.CSharp` | xUnit |
| Contract | Public F# and C# surfaces | PublicApiGenerator or reflection |
| Property | URL, route table and history | FsCheck |

### Required edge cases

- Empty route table, duplicate names, invalid patterns, ambiguous patterns, static against parameter and wildcard.
- Optional query, repeated query, hash, trailing slash, unicode, encoded characters and invalid URL.
- Global, activation and deactivation guards; stop, redirect, exception and cancellation at each stage.
- Redirect chain, cycle, maximum, missing target, guard redirect against declarative redirect.
- History bounds, forward truncation, guard-blocked back with index restore, same URL and external `popstate`.
- `Idle` after success, error, cancellation and exception; pre-cancelled token; cancellation during guard, resolver, activation and commit; concurrent and nested navigation.
- Cache hit, miss, hit with guard, `NoCache` disposal, eviction and invalidation.
- Stable error codes; `CorrelationId` in log and response; no exception message or internal type leakage.
- Splash, no content, transition, outlet replacement, old content disposal, UI thread, active links and two routers with two outlets.
- C# `NavigationOutcome`, `NavigationErrorCode`, invalid configuration, cancellation and nullability.

## 8. Tracking

| Phase | Status | Owner | Notes |
| --- | --- | --- | --- |
| 0. Contracts and DSL | Done | | Contracts, guard order and F#/C# surface tests pass |
| 1. URL and route table | Done | | Parse/rank/match/encoding, ambiguity, edge-case tables and FsCheck URL property pass |
| 2. Coordinator and state | Done | | Single state owner, cancellation, supersession and all concurrency policies tested |
| 3. Guards and redirects | Done | | Guard order, stop, guard redirects, declarative redirects and cycles tested |
| 4. History | Done | | Push/replace/commit, back/forward, guard rollback and same-URL policies tested |
| 5. Views and cache | Done | | View activation, TTL, bounded eviction and explicit `ClearCache` tested; resolver pipeline remains optional |
| 6. F# facade and DSL | Done | | `Route.define`, `CancellableTask`, `.fsi` and surface tests pass |
| 7. C# facade | Done | | C# facade, guards, cancellation and 8 xUnit tests pass |
| 8. Adapters | Done | | Adapter tests, docs and all five samples migrated; full headless interaction suite deferred, headless startup covered |

Tracking rules:

1. One phase is `In progress` at a time unless two phases are independent.
2. A task is checked only when its test or compile check passes.
3. A phase is `Done` only when its exit criteria and the relevant contract tests pass.
4. Update this file in the same commit or pull request that changes the phase status.
5. Do not add a new public type or member without updating the public-surface tests.
6. Do not introduce `ValueTask` into a public signature.
7. Do not add a separate `CancellableTask` route overload.
8. Keep the C# surface free of F# types.

## 9. Out of scope for phases 0–8

These are web features for a later phase, after the core and adapters are stable:

- Nested routes and layout chains.
- Route-level metadata and resolvers beyond the optional resolver pipeline.
- Lazy-loaded views.
- Browser history adapter and `popstate` integration.
- Scroll restoration.
- Active-link helpers beyond `IRouteLinks`.
