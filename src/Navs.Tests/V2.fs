module Navs.Tests.V2

open System
open System.Collections.Generic
open System.Reflection
open System.Threading
open System.Threading.Tasks

open Expecto
open FsCheck
open FsCheck.FSharp

open Navs
open Navs.Router

let config routes = {
  Routes = routes
  History = InMemoryHistory()
  Splash = None
  Guards = []
  Cache = NoCache
  Concurrency = LatestWins
  SameUrl = Ignore
  MaxRedirects = 5
  Logger = None
}

let buildWithConfig routerConfig =
  match Router.create routerConfig with
  | Ok router -> router
  | Error errors -> failtestf "Route table failed: %A" errors

let build routes = buildWithConfig(config routes)

let navigate (router: IRouter<string>) url =
  router.Navigate (url) CancellationToken.None
  |> fun task -> task.GetAwaiter().GetResult()

let expectOk result =
  match result with
  | Ok value -> value
  | Error error -> failtestf "expected Ok, got %A" error

let route (name: string) (path: string) (view: string) =
  Route.define<string>(name, path, fun _ _ -> view)

let buildWithSameUrl sameUrl routes =
  buildWithConfig { config routes with SameUrl = sameUrl }

let buildWith concurrency routes =
  buildWithConfig {
    config routes with
        Concurrency = concurrency
  }

type RecordingSynchronizationContext() as this =
  inherit SynchronizationContext()
  let mutable postCount = 0

  member _.PostCount = Volatile.Read(&postCount)

  override _.Post(callback, state) =
    Interlocked.Increment(&postCount) |> ignore

    ThreadPool.QueueUserWorkItem(fun _ ->
      SynchronizationContext.SetSynchronizationContext this
      callback.Invoke state
    )
    |> ignore

[<Tests>]
let tests =
  testList "Navs v2" [
    testCase "async handlers resume on the caller SynchronizationContext"
    <| fun () ->
      let context = RecordingSynchronizationContext()
      let mutable startedOnContext = false
      let mutable resumedOnContext = false

      let routes = [
        Route.define<string>(
          "home",
          "/",
          fun _ _ -> async {
            startedOnContext <-
              Object.ReferenceEquals(SynchronizationContext.Current, context)

            do! Async.Sleep 10

            resumedOnContext <-
              Object.ReferenceEquals(SynchronizationContext.Current, context)

            return "home"
          }
        )
      ]

      let router = build routes
      let previous = SynchronizationContext.Current

      SynchronizationContext.SetSynchronizationContext context

      try
        navigate router "/" |> expectOk |> ignore
      finally
        SynchronizationContext.SetSynchronizationContext previous

      Expect.isTrue
        startedOnContext
        "the handler should start on the caller context"

      Expect.isTrue
        resumedOnContext
        "the handler should resume on the caller context"

      Expect.isGreaterThan
        context.PostCount
        0
        "the handler should post a continuation"

    testCase "async guards resume on the caller SynchronizationContext"
    <| fun () ->
      let context = RecordingSynchronizationContext()
      let mutable resumedOnContext = false

      let guard _ _ = async {
        do! Async.Sleep 10

        resumedOnContext <-
          Object.ReferenceEquals(SynchronizationContext.Current, context)

        return Continue
      }

      let routes = [ route "home" "/" "home" |> Route.canActivateAsync guard ]

      let router = build routes
      let previous = SynchronizationContext.Current

      SynchronizationContext.SetSynchronizationContext context

      try
        navigate router "/" |> expectOk |> ignore
      finally
        SynchronizationContext.SetSynchronizationContext previous

      Expect.isTrue
        resumedOnContext
        "the guard should resume on the caller context"

      Expect.isGreaterThan
        context.PostCount
        0
        "the guard should post a continuation"

    testCase "static routes rank before parameter routes"
    <| fun () ->
      let routes = [
        Route.define<string>(
          "user",
          "/:id",
          fun context _ ->
            context.GetParam<string>("id") |> ValueOption.defaultValue "none"
        )
        route "about" "/about" "about"
      ]

      let router = build routes
      let result = navigate router "/about"

      Expect.isOk result "navigation should succeed"

      Expect.equal
        router.Snapshot.Content
        (ValueSome "about")
        "the static route should win"

      Expect.equal router.Snapshot.State Idle "state should be Idle"

    testCase "a stopped deactivation guard leaves the router Idle"
    <| fun () ->
      let routes = [
        route "home" "/" "home" |> Route.canDeactivate(fun _ _ -> Stop)
        route "about" "/about" "about"
      ]

      let router = build routes
      navigate router "/" |> ignore
      let result = navigate router "/about"

      match result with
      | Error(GuardStopped "home") -> ()
      | other -> failtestf "expected GuardStopped, got %A" other

      Expect.equal router.Snapshot.State Idle "state should return to Idle"

      Expect.equal
        router.Snapshot.Content
        (ValueSome "home")
        "the active view should not change"

    testCase "guards run in registration order"
    <| fun () ->
      let order = ResizeArray<string>()

      let routes = [
        route "home" "/" "home"
        |> Route.canActivate(fun _ _ ->
          order.Add "first"
          Continue
        )
        |> Route.canActivate(fun _ _ ->
          order.Add "second"
          Continue
        )
      ]

      let router = build routes
      navigate router "/" |> ignore

      Expect.sequenceEqual
        (List.ofSeq order)
        [ "first"; "second" ]
        "guards should run in registration order"

    testCase "back and forward move through history"
    <| fun () ->
      let routes = [ route "home" "/" "home"; route "about" "/about" "about" ]
      let router = build routes

      navigate router "/" |> ignore
      navigate router "/about" |> ignore

      Expect.equal
        router.Snapshot.History.Index
        1
        "history should be at the second entry"

      router.Back () CancellationToken.None
      |> fun task -> task.GetAwaiter().GetResult()
      |> ignore

      Expect.equal
        router.Snapshot.History.Index
        0
        "back should move to the first entry"

      Expect.equal
        router.Snapshot.Content
        (ValueSome "home")
        "back should restore the home view"

      router.Forward () CancellationToken.None
      |> fun task -> task.GetAwaiter().GetResult()
      |> ignore

      Expect.equal
        router.Snapshot.History.Index
        1
        "forward should move to the second entry"

      Expect.equal
        router.Snapshot.Content
        (ValueSome "about")
        "forward should restore the about view"

    testCase "leaving a NoCache route disposes its resources"
    <| fun () ->
      let mutable disposed = false

      let disposable =
        { new IDisposable with
            member _.Dispose() = disposed <- true
        }

      let routes = [
        Route.define<string>(
          "home",
          "/",
          fun context _ ->
            context.AddDisposable disposable
            "home"
        )
        Route.define<string>("about", "/about", fun _ _ -> "about")
      ]

      let router = build routes
      navigate router "/" |> ignore

      Expect.isFalse
        disposed
        "resources should stay alive while the route is active"

      navigate router "/about" |> ignore

      Expect.isTrue
        disposed
        "leaving a NoCache route should dispose its resources"

    testCase "a guard-blocked back keeps the history index"
    <| fun () ->
      let routes = [
        route "home" "/" "home"
        route "about" "/about" "about" |> Route.canDeactivate(fun _ _ -> Stop)
      ]

      let router = build routes
      navigate router "/" |> ignore
      navigate router "/about" |> ignore

      let result =
        router.Back () CancellationToken.None
        |> fun task -> task.GetAwaiter().GetResult()

      match result with
      | Error(GuardStopped "about") -> ()
      | other -> failtestf "expected GuardStopped about, got %A" other

      Expect.equal
        router.Snapshot.History.Index
        1
        "a blocked back should not move the history index"

    testCase "same URL ignore keeps one history entry"
    <| fun () ->
      let routes = [ route "home" "/" "home" ]
      let router = buildWithSameUrl Ignore routes

      navigate router "/" |> ignore
      navigate router "/" |> ignore

      Expect.equal
        router.Snapshot.History.Index
        0
        "the second navigation should not push history"

      Expect.equal
        router.Snapshot.History.Entries.Count
        1
        "same URL should keep a single entry"

    testCase "same URL push adds a history entry"
    <| fun () ->
      let routes = [ route "home" "/" "home" ]
      let router = buildWithSameUrl Push routes

      navigate router "/" |> ignore
      navigate router "/" |> ignore

      Expect.equal
        router.Snapshot.History.Index
        1
        "the second navigation should push history"

      Expect.equal
        router.Snapshot.History.Entries.Count
        2
        "same URL push should create two entries"

    testCase "a declarative redirect resolves the target and replaces history"
    <| fun () ->
      let routes = [
        route "old" "/old" "old" |> Route.redirect(NavigationTarget.Url "/new")
        route "new" "/new" "new"
      ]

      let router = build routes
      let result = navigate router "/old"

      let navigation = expectOk result

      Expect.equal navigation.Route.Path "/new" "the target should be active"

      Expect.sequenceEqual
        navigation.Redirects
        [ "/old" ]
        "the chain should contain the old URL"

      Expect.equal
        router.Snapshot.Content
        (ValueSome "new")
        "the target view should render"

      Expect.equal
        router.Snapshot.History.Index
        0
        "the redirect should replace history"

      match router.Snapshot.History.Current with
      | Some current ->
        Expect.equal current.Url "/new" "history should store the target"
      | None -> failtest "history should contain the target entry"

    testCase "a guard redirect resolves the target"
    <| fun () ->
      let routes = [
        route "private" "/private" "private"
        |> Route.canActivate(fun _ _ -> Redirect "/login")
        route "login" "/login" "login"
      ]

      let router = build routes
      let result = navigate router "/private"

      let navigation = expectOk result

      Expect.equal
        navigation.Route.Path
        "/login"
        "the login route should be active"

      Expect.equal
        router.Snapshot.Content
        (ValueSome "login")
        "the login view should render"

    testCase "a redirect cycle returns RedirectLoop"
    <| fun () ->
      let routes = [
        route "a" "/a" "a" |> Route.redirect(NavigationTarget.Url "/b")
        route "b" "/b" "b" |> Route.redirect(NavigationTarget.Url "/a")
      ]

      let router = build routes

      match navigate router "/a" with
      | Error(RedirectLoop _) -> ()
      | other -> failtestf "expected RedirectLoop, got %A" other

    testCase "a long guard redirect chain does not overflow the stack"
    <| fun () ->
      let routes = [
        route "loop" "/loop" "loop"
        |> Route.canActivate(fun _ _ -> Redirect "/loop")
      ]

      let router =
        buildWithConfig {
          config routes with
              MaxRedirects = 10000
        }

      match navigate router "/loop" with
      | Error(RedirectLoop _) -> ()
      | other -> failtestf "expected RedirectLoop, got %A" other

    testCase "cached routes do not re-execute the view"
    <| fun () ->
      let mutable count = 0

      let routes = [
        Route.define<string>(
          "home",
          "/",
          fun _ _ ->
            count <- count + 1
            "home"
        )
        |> Route.cache(Cache(10, None))
      ]

      let router = build routes
      navigate router "/" |> ignore
      navigate router "/" |> ignore

      Expect.equal count 1 "the cached view should be reused"

    testCase "ClearCache forces the view to be recreated"
    <| fun () ->
      let mutable count = 0

      let routes = [
        Route.define<string>(
          "home",
          "/",
          fun _ _ ->
            count <- count + 1
            "home"
        )
        |> Route.cache(Cache(10, None))
      ]

      let router = build routes
      navigate router "/" |> ignore
      navigate router "/" |> ignore
      Expect.equal count 1 "the cached view should be reused"

      router.ClearCache()
      navigate router "/" |> ignore

      Expect.equal count 2 "ClearCache should force a new view"

    testCase "cache expires entries by ttl"
    <| fun () ->
      let mutable count = 0

      let routes = [
        Route.define<string>(
          "home",
          "/",
          fun _ _ ->
            count <- count + 1
            "home"
        )
        |> Route.cache(Cache(10, Some(TimeSpan.FromMilliseconds 10.)))
      ]

      let router = build routes
      navigate router "/" |> ignore
      Task.Delay(50).GetAwaiter().GetResult()
      navigate router "/" |> ignore

      Expect.equal count 2 "the expired view should be recreated"

    testCase "cache evicts the oldest entry when the limit is reached"
    <| fun () ->
      let mutable homeCount = 0
      let mutable aboutCount = 0

      let routes = [
        Route.define<string>(
          "home",
          "/",
          fun _ _ ->
            homeCount <- homeCount + 1
            "home"
        )
        |> Route.cache(Cache(1, None))
        Route.define<string>(
          "about",
          "/about",
          fun _ _ ->
            aboutCount <- aboutCount + 1
            "about"
        )
        |> Route.cache(Cache(1, None))
      ]

      let router = build routes
      navigate router "/" |> ignore
      navigate router "/about" |> ignore
      navigate router "/" |> ignore

      Expect.equal homeCount 2 "home should be evicted and recreated"
      Expect.equal aboutCount 1 "about should remain cached"

    testCase "a pre-cancelled token returns Cancelled and leaves Idle"
    <| fun () ->
      let router = build [ route "home" "/" "home" ]
      use cts = new CancellationTokenSource()
      cts.Cancel()

      let result =
        router.Navigate ("/") cts.Token
        |> fun task -> task.GetAwaiter().GetResult()

      match result with
      | Error Cancelled -> ()
      | other -> failtestf "expected Cancelled, got %A" other

      Expect.equal
        router.Snapshot.State
        Idle
        "state should be Idle after cancellation"

    testCase "invalid route tables fail at creation"
    <| fun () ->
      let duplicate = [ route "home" "/" "home"; route "home" "/other" "other" ]

      match Router.create(config duplicate) with
      | Error [ DuplicateRouteName "home" ] -> ()
      | other -> failtestf "expected duplicate route error, got %A" other

      let invalid = [ route "bad" "/:id<int" "bad" ]

      match Router.create(config invalid) with
      | Error [ InvalidPattern _ ] -> ()
      | other -> failtestf "expected invalid pattern error, got %A" other

      let ambiguous = [
        route "first" "/users/:id" "first"
        route "second" "/users/:name" "second"
      ]

      match Router.create(config ambiguous) with
      | Error [ AmbiguousPattern(first, second) ] ->
        Expect.equal first "/users/:id" "the first pattern should be reported"

        Expect.equal
          second
          "/users/:name"
          "the second pattern should be reported"
      | other -> failtestf "expected ambiguous pattern error, got %A" other

    testCase "links build and report the active route"
    <| fun () ->
      let routes = [
        Route.define<string>("user", "/users/:id<int>", fun _ _ -> "user")
      ]

      let router = build routes
      let parameters = Dictionary<string, obj>()
      parameters["id"] <- 42 :> obj

      match router.Links.TryBuild("user", parameters) with
      | Ok "/users/42" -> ()
      | other -> failtestf "expected /users/42, got %A" other

      navigate router "/users/42" |> ignore

      Expect.isTrue
        (router.Links.IsActive("user", parameters))
        "the user link should be active"

    testCase "link builder encodes values and skips missing optional query"
    <| fun () ->
      let routes = [
        Route.define<string>("search", "/search?q", fun _ _ -> "search")
        Route.define<string>("user", "/users/:id", fun _ _ -> "user")
      ]

      let router = build routes

      let empty = Dictionary<string, obj>()

      Expect.equal
        (router.Links.TryBuild("search", empty))
        (Ok "/search")
        "missing optional query should be omitted"

      let parameters = Dictionary<string, obj>()
      parameters["id"] <- "a b" :> obj

      Expect.equal
        (router.Links.TryBuild("user", parameters))
        (Ok "/users/a%20b")
        "path values should be encoded"

    testCase "URL builder round-trips encoded path values"
    <| fun () ->
      let mutable seen = ""

      let routes = [
        Route.define<string>(
          "user",
          "/users/:id",
          fun context _ ->
            seen <-
              context.GetParam<string>("id") |> ValueOption.defaultValue ""

            "user"
        )
      ]

      let router = build routes
      let parameters = Dictionary<string, obj>()
      parameters["id"] <- "a b" :> obj

      let url = router.Links.TryBuild("user", parameters) |> expectOk

      Expect.equal url "/users/a%20b" "the URL should be encoded"
      navigate router url |> ignore

      Expect.equal seen "a b" "the parameter should be decoded after navigation"

    testCase "query values decode plus and keep repeated values"
    <| fun () ->
      let seen = ResizeArray<string>()

      let routes = [
        Route.define<string>(
          "search",
          "/search?q",
          fun context _ ->
            seen.AddRange(context.GetParamSequence<string>("q"))
            "search"
        )
      ]

      let router = build routes
      navigate router "/search?q=hello+world&q=second" |> ignore

      Expect.sequenceEqual
        (seen |> Seq.sort |> List.ofSeq)
        [ "hello world"; "second" ]
        "query values should be decoded and preserved"

    testCase "hash values are decoded"
    <| fun () ->
      let mutable hash = ""

      let routes = [
        Route.define<string>(
          "docs",
          "/docs",
          fun context _ ->
            hash <- context.UrlInfo.Hash |> ValueOption.defaultValue ""

            "docs"
        )
      ]

      let router = build routes
      navigate router "/docs#intro%20section" |> ignore

      Expect.equal hash "intro section" "the hash should be decoded"

    testCase "URL builder round-trips arbitrary path values"
    <| fun () ->
      let mutable seen = ""

      let routes = [
        Route.define<string>(
          "user",
          "/users/:id",
          fun context _ ->
            seen <-
              context.GetParam<string>("id") |> ValueOption.defaultValue ""

            "user"
        )
      ]

      let router = build routes

      let values =
        ArbMap.defaults
        |> ArbMap.arbitrary<string>
        |> Arb.filter(fun value -> not(String.IsNullOrEmpty value))

      let property =
        Prop.forAll
          values
          (fun value ->
            let parameters = Dictionary<string, obj>()
            parameters["id"] <- value :> obj

            match router.Links.TryBuild("user", parameters) with
            | Error _ -> false
            | Ok url ->
              seen <- ""
              navigate router url |> ignore
              seen = value
          )

      Check.One(Config.QuickThrowOnFailure, property)

    testCase "Reject returns Superseded while another navigation is active"
    <| fun () ->
      let started =
        TaskCompletionSource<unit>(
          TaskCreationOptions.RunContinuationsAsynchronously
        )

      let release =
        TaskCompletionSource<unit>(
          TaskCreationOptions.RunContinuationsAsynchronously
        )

      let routes = [
        Route.define<string>(
          "slow",
          "/slow",
          fun _ _ _ -> task {
            started.TrySetResult(()) |> ignore
            do! release.Task
            return "slow"
          }
        )
        route "other" "/other" "other"
      ]

      let router = buildWith Reject routes
      let first = router.Navigate ("/slow") CancellationToken.None

      started.Task.GetAwaiter().GetResult()

      let second =
        router.Navigate ("/other") CancellationToken.None
        |> fun task -> task.GetAwaiter().GetResult()

      match second with
      | Error Superseded -> ()
      | other -> failtestf "expected Superseded, got %A" other

      release.TrySetResult(()) |> ignore

      Expect.isOk
        (first.GetAwaiter().GetResult())
        "the first navigation should complete"

    testCase "Serialize queues a navigation until the active one completes"
    <| fun () ->
      let started =
        TaskCompletionSource<unit>(
          TaskCreationOptions.RunContinuationsAsynchronously
        )

      let release =
        TaskCompletionSource<unit>(
          TaskCreationOptions.RunContinuationsAsynchronously
        )

      let routes = [
        Route.define<string>(
          "slow",
          "/slow",
          fun _ _ _ -> task {
            started.TrySetResult(()) |> ignore
            do! release.Task
            return "slow"
          }
        )
        route "other" "/other" "other"
      ]

      let router = buildWith Serialize routes
      let first = router.Navigate ("/slow") CancellationToken.None

      started.Task.GetAwaiter().GetResult()

      let second = router.Navigate ("/other") CancellationToken.None
      Task.Delay(50).GetAwaiter().GetResult()

      Expect.isFalse second.IsCompleted "the second navigation should be queued"

      release.TrySetResult(()) |> ignore

      Expect.isOk
        (first.GetAwaiter().GetResult())
        "the first navigation should complete"

      Expect.isOk
        (second.GetAwaiter().GetResult())
        "the second navigation should complete"

    testCase "public surface excludes ValueTask and internal work types"
    <| fun () ->
      let assembly = typeof<IRouter<string>>.Assembly
      let publicTypes = assembly.GetExportedTypes()

      let rec containsValueTask (candidate: Type) =
        let name =
          candidate.FullName
          |> Option.ofObj
          |> Option.defaultValue candidate.Name

        name.Contains("ValueTask")
        || (candidate.IsGenericType
            && candidate.GetGenericArguments() |> Array.exists containsValueTask)

      let methods =
        publicTypes
        |> Array.collect(fun publicType ->
          publicType.GetMethods(
            BindingFlags.Public
            ||| BindingFlags.Instance
            ||| BindingFlags.Static
            ||| BindingFlags.DeclaredOnly
          )
        )

      let exposed =
        methods
        |> Array.collect(fun methodInfo ->
          Array.append
            [| methodInfo.ReturnType |]
            (methodInfo.GetParameters()
             |> Array.map(fun parameter -> parameter.ParameterType))
        )
        |> Array.filter containsValueTask

      Expect.isEmpty exposed "public signatures should not expose ValueTask"

      let internalWorkTypes =
        publicTypes
        |> Array.filter(fun publicType ->
          publicType.Name.StartsWith("ViewWork")
          || publicType.Name.StartsWith("GuardWork")
        )

      Expect.isEmpty
        internalWorkTypes
        "internal work types should not be exported"
  ]
