module GooTests

open System
open System.Collections.Generic
open System.Threading
open Microsoft.Extensions.Logging
open Expecto
open Goo
open Navs
open Navs.Router
open Navs.Goo

let navigate (router: IRouter<Blob>, url: string) =
  router.Navigate (url) CancellationToken.None
  |> fun task -> task.GetAwaiter().GetResult()

let textOf (blob: Blob) =
  match blob with
  | :? Text as text -> text.Content
  | :? Container as container ->
    container.Children
    |> Seq.tryHead
    |> Option.map(fun child ->
      match child with
      | :? Text as text -> text.Content
      | _ -> "<not-text>"
    )
    |> Option.defaultValue "<empty>"
  | _ -> "<other>"

let homeAboutRoutes () = [
  Route.define<Blob>("home", "/", fun _ _ -> Text(Content = "Home") :> Blob)
  Route.define<Blob>(
    "about",
    "/about",
    fun _ _ -> Text(Content = "About") :> Blob
  )
  Route.define<Blob>(
    "item",
    "/items/:id<int>",
    fun context _ ->
      let id =
        match context.GetParam<int>("id") with
        | ValueSome value -> string value
        | ValueNone -> "?"

      Text(Content = $"Item {id}") :> Blob
  )
]

type TestLogger(logs: ResizeArray<string>) =
  interface ILogger with
    member _.IsEnabled(_) = true

    member _.BeginScope<'TState when 'TState: not null>
      (_: 'TState)
      : IDisposable =
      { new IDisposable with
          member _.Dispose() = ()
      }

    member _.Log<'TState>
      (
        _: LogLevel,
        _: EventId,
        state: 'TState,
        ex: exn,
        formatter: Func<'TState, exn, string>
      ) =
      logs.Add(formatter.Invoke(state, ex))

let waitFor (condition: unit -> bool) =
  let timer = System.Diagnostics.Stopwatch.StartNew()

  while not(condition()) && timer.Elapsed < TimeSpan.FromSeconds(5.0) do
    Thread.Sleep(10)

  condition()

[<Tests>]
let tests =
  testList "Navs.Goo" [
    testCase "router and outlet render the active view"
    <| fun () ->
      let router = GooRouter(homeAboutRoutes()) :> IRouter<Blob>

      Expect.isOk (navigate(router, "/")) "initial navigation should succeed"

      let outlet = new RouterOutlet()
      outlet.Router <- Some router

      Expect.equal
        (textOf(outlet.Build()))
        "Home"
        "the outlet should render Home"

      Expect.isOk
        (navigate(router, "/about"))
        "second navigation should succeed"

      Expect.equal
        (textOf(outlet.Build()))
        "About"
        "the outlet should render About"

      Expect.isOk
        (navigate(router, "/items/42"))
        "item navigation should succeed"

      Expect.equal
        (textOf(outlet.Build()))
        "Item 42"
        "the outlet should render the item"

      Expect.isOk
        (router.Back () CancellationToken.None
         |> fun task -> task.GetAwaiter().GetResult())
        "back should succeed"

      Expect.equal
        (textOf(outlet.Build()))
        "About"
        "the outlet should render About after back"

    testCase "outlet renders splash and no-content fallback"
    <| fun () ->
      let splashRouter =
        GooRouter(
          homeAboutRoutes(),
          splash = Func<Blob>(fun () -> Text(Content = "Splash") :> Blob)
        )
        :> IRouter<Blob>

      let outlet = new RouterOutlet()
      outlet.Router <- Some splashRouter

      Expect.equal
        (textOf(outlet.Build()))
        "Splash"
        "the outlet should render the splash"

      Expect.isOk (navigate(splashRouter, "/")) "navigation should succeed"

      Expect.equal
        (textOf(outlet.Build()))
        "Home"
        "the outlet should render Home"

      let emptyRouter = GooRouter(homeAboutRoutes()) :> IRouter<Blob>
      let emptyOutlet = new RouterOutlet()
      emptyOutlet.NoContent <- Some(Text(Content = "No content") :> Blob)
      emptyOutlet.Router <- Some emptyRouter

      Expect.equal
        (textOf(emptyOutlet.Build()))
        "No content"
        "the outlet should render NoContent"

    testCase "outlet unsubscribes from the previous router"
    <| fun () ->
      let routerA = GooRouter(homeAboutRoutes()) :> IRouter<Blob>
      let routerB = GooRouter(homeAboutRoutes()) :> IRouter<Blob>

      Expect.isOk (navigate(routerA, "/")) "router A should navigate"
      Expect.isOk (navigate(routerB, "/about")) "router B should navigate"

      let outlet = new RouterOutlet()
      outlet.Router <- Some routerA

      Expect.equal
        (textOf(outlet.Build()))
        "Home"
        "the outlet should render router A"

      outlet.Router <- Some routerB

      Expect.equal
        (textOf(outlet.Build()))
        "About"
        "the outlet should render router B"

      Expect.isOk (navigate(routerA, "/about")) "router A should move"

      Expect.equal
        (textOf(outlet.Build()))
        "About"
        "the outlet should still render router B"

      Expect.isOk (navigate(routerB, "/")) "router B should move"

      Expect.equal
        (textOf(outlet.Build()))
        "Home"
        "the outlet should render the new router B view"

    testCase "stateful route returns a mount blob and keeps Cell ownership"
    <| fun () ->
      let mutable builds = 0
      let mutable disposed = false

      let makeCell () =
        { new Cell() with
            override _.Build() : Blob =
              builds <- builds + 1
              Text(Content = "Stateful") :> Blob

          interface IDisposable with
            member _.Dispose() = disposed <- true
        }

      let routes = [
        Route.define<Blob>(
          "stateful",
          "/stateful",
          fun _ _ ->
            Cell.Mount<Cell>(Func<Cell>(fun () -> makeCell()), "stateful")
        )
      ]

      let router = GooRouter(routes) :> IRouter<Blob>

      Expect.isOk (navigate(router, "/stateful")) "navigation should succeed"

      let blobType =
        router.Snapshot.Content
        |> ValueOption.map(fun blob -> blob.GetType().FullName)
        |> ValueOption.defaultValue "<none>"

      Expect.equal blobType "Goo.CellElement" "the view should be a mount blob"
      Expect.equal builds 0 "the router should not build the Cell"
      Expect.isFalse disposed "the router should not dispose the Cell"

    testCase "route table error text keeps union fields"
    <| fun () ->
      let badRoutes = [
        Route.define<Blob>("home", "/", fun _ _ -> Text(Content = "A") :> Blob)
        Route.define<Blob>(
          "home",
          "/other",
          fun _ _ -> Text(Content = "B") :> Blob
        )
      ]

      let message =
        try
          let _ = GooRouter(badRoutes)
          "<no-error>"
        with :? InvalidOperationException as ex ->
          ex.Message

      Expect.stringContains
        message
        "DuplicateRouteName home"
        "the error text should keep the route name"

    testCase "initial navigation error log keeps union fields"
    <| fun () ->
      let logs = ResizeArray<string>()
      let logger = TestLogger(logs) :> ILogger
      let router = GooRouter(homeAboutRoutes()) :> IRouter<Blob>

      let outlet = new RouterOutlet(initialUri = "/missing", logger = logger)
      outlet.Router <- Some router
      outlet.Start()

      let found =
        waitFor(fun () ->
          logs
          |> Seq.exists(fun line -> line.Contains("RouteNotFound /missing"))
        )

      Expect.isTrue found "the log should keep the missing URL"
  ]
