open System
open System.Threading
open System.Threading.Tasks

open IcedTasks
open Avalonia.Controls
open NXUI.Desktop
open NXUI.Extensions

open Navs
open Navs.Router
open Navs.Avalonia
open Microsoft.Extensions.Logging

let lf =
  LoggerFactory.Create(fun builder ->
    builder.AddConsole().SetMinimumLevel LogLevel.Trace |> ignore
  )

let logger = lf.CreateLogger "Navs.Avalonia.Program"

let block text = TextBlock().Text(text) :> Control

let syncGuard: SyncGuard =
  fun _ next ->
    logger.LogInformation("Sync guard passed for {Path}", next.Path)
    Continue

let asyncGuard: AsyncGuard =
  fun _ next -> async {
    do! Async.Sleep 10
    logger.LogInformation("Async guard passed for {Path}", next.Path)
    return Continue
  }

let taskGuard: TaskGuard =
  fun _ next token -> task {
    do! Task.Delay(10, token)
    logger.LogInformation("Task guard passed for {Path}", next.Path)
    return Continue
  }

let cancellableGuard: TaskGuard =
  fun _ next -> cancellableTask {
    let! token = CancellableTask.getCancellationToken()
    do! Task.Delay(10, token)
    logger.LogInformation("Cancellable guard passed for {Path}", next.Path)
    return Continue
  }

let routes = [
  // 1. Sync handler and sync guard.
  Route.define<Control>("books", "/books", fun _ _ -> block "Books")
  |> Route.canActivate syncGuard
  |> Route.canDeactivate syncGuard

  // 2. Async handler and async guard.
  Route.define<Control>(
    "guid",
    "/:id<guid>",
    fun context _ -> async {
      do! Async.Sleep 10

      return
        match context.GetParam<Guid>("id") with
        | ValueSome id -> block $"%O{id}"
        | ValueNone -> block "Guid No GUID"
    }
  )
  |> Route.canActivateAsync asyncGuard
  |> Route.canDeactivateAsync asyncGuard

  // 3. Task handler and task guard.
  Route.define<Control>(
    "task",
    "/task",
    fun _ _ token -> task {
      do! Task.Delay(10, token)
      return block "Task route"
    }
  )
  |> Route.canActivateTask taskGuard
  |> Route.canDeactivateTask taskGuard

  // 4. Cancellable task handler and cancellable task guard.
  Route.define<Control>(
    "cancellable",
    "/cancellable",
    fun _ _ -> cancellableTask {
      let! token = CancellableTask.getCancellationToken()
      do! Task.Delay(10, token)
      return block "Cancellable route"
    }
  )
  |> Route.canActivateTask cancellableGuard
  |> Route.canDeactivateTask cancellableGuard

  Route.define<Control>(
    "counter",
    "/counter?count<int>",
    fun context _ ->
      let mutable count =
        context.GetParam<int>("count") |> ValueOption.defaultValue 0

      let text = TextBlock().Text($"Count: {count}")

      let update () = text.Text <- $"Count: {count}"

      StackPanel()
        .Spacing(8)
        .Children(
          Button()
            .Content("Increment")
            .OnClickHandler(fun _ _ ->
              count <- count + 1
              update()
            ),
          Button()
            .Content("Decrement")
            .OnClickHandler(fun _ _ ->
              count <- count - 1
              update()
            ),
          Button()
            .Content("Reset")
            .OnClickHandler(fun _ _ ->
              count <- 0
              update()
            ),
          text
        )
      :> Control
  )
  |> Route.cache NoCache
]

let navigate (url: string) (router: IRouter<Control>) =
  task {
    match! router.Navigate url CancellationToken.None with
    | Ok _ -> ()
    | Error error ->
      logger.LogError("Navigation failed for {Url}: {Error}", url, error)
  }
  |> ignore

let app () =
  let router =
    AvaloniaRouter(
      routes,
      splash = Func<Control>(fun () -> block "Loading..."),
      logger = logger
    )
    :> IRouter<Control>

  let outlet = RouterOutlet()
  outlet.Router <- Some router

  Window()
    .Content(
      DockPanel()
        .LastChildFill(true)
        .Children(
          StackPanel()
            .DockTop()
            .OrientationHorizontal()
            .Spacing(8)
            .Children(
              Button()
                .Content("Sync")
                .OnClickHandler(fun _ _ -> navigate "/books" router),
              Button()
                .Content("Async")
                .OnClickHandler(fun _ _ -> navigate $"/{Guid.NewGuid()}" router),
              Button()
                .Content("Task")
                .OnClickHandler(fun _ _ -> navigate "/task" router),
              Button()
                .Content("Cancellable")
                .OnClickHandler(fun _ _ -> navigate "/cancellable" router),
              Button()
                .Content("Counter")
                .OnClickHandler(fun _ _ -> navigate "/counter" router),
              Button()
                .Content("Counter with query")
                .OnClickHandler(fun _ _ -> navigate "/counter?count=10" router)
            ),
          outlet.DockTop()
        )
    )

NXUI.Run(app, "Navs.Avalonia!", Environment.GetCommandLineArgs()) |> ignore
