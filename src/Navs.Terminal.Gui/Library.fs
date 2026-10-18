namespace Navs.Terminal.Gui

open System
open System.Runtime.InteropServices
open System.Threading
open Microsoft.Extensions.Logging
open Terminal.Gui
open Terminal.Gui.Views
open Navs
open Navs.Router

type TerminalGuiRouter
  (
    routes: RouteDefinition<Window> seq,
    [<Optional>] ?splash: Func<Window>,
    [<Optional>] ?logger: ILogger
  ) =

  let router =
    let config = {
      Routes = List.ofSeq routes
      History = InMemoryHistory()
      Splash = splash |> Option.map(fun create -> create.Invoke())
      Guards = []
      Cache = NoCache
      Concurrency = LatestWins
      SameUrl = Ignore
      MaxRedirects = 5
      Logger = logger
    }

    match Router.create config with
    | Ok value -> value
    | Error errors -> invalidOp $"Invalid route table: {errors}"

  interface IRouter<Window> with
    member _.Snapshot = router.Snapshot
    member _.Changed = router.Changed
    member _.History = router.History
    member _.Links = router.Links
    member _.ClearCache() = router.ClearCache()
    member _.Navigate(url) = router.Navigate(url)

    member _.NavigateByName(name, parameters) =
      router.NavigateByName(name, parameters)

    member _.Back() = router.Back()
    member _.Forward() = router.Forward()

type RouterOutlet
  (
    router: IRouter<Window>,
    [<Optional>] ?initialUri: string,
    [<Optional>] ?logger: ILogger
  ) as this =
  inherit Runnable()

  do
    let logNavigationError (uri: string) (error: NavigationError) =
      match logger with
      | Some log ->
        log.LogError(
          "Failed to navigate to initial URI {Uri}: {Error}",
          uri,
          error
        )
      | None -> eprintfn "Failed to navigate to initial URI %s: %A" uri error

    let handler =
      fun _ (snapshot: RouterSnapshot<Window>) ->
        this.RemoveAll() |> ignore

        match snapshot.Content with
        | ValueSome window -> this.Add(window :> ViewBase.View) |> ignore
        | ValueNone -> ()

    router.Changed.AddHandler handler
    this.Disposing.Add(fun _ -> router.Changed.RemoveHandler handler)

    let uri = defaultArg initialUri "/"

    task {
      match! router.Navigate uri CancellationToken.None with
      | Ok _ -> ()
      | Error error -> logNavigationError uri error
    }
    |> ignore
