namespace Navs.Goo

open System
open System.Runtime.InteropServices
open System.Threading
open Microsoft.Extensions.Logging
open Goo
open Navs
open Navs.Router

module Helpers =

  // Match each union case and use only plain fields. A union, a tuple, a
  // map, or a list of unions in one generic text hole can lose its fields
  // in a native build, and a classic format string can throw there.
  let formatRouteError (error: RouteTableError) =
    match error with
    | EmptyRoutes -> "EmptyRoutes"
    | EmptyRouteName -> "EmptyRouteName"
    | DuplicateRouteName name -> $"DuplicateRouteName {name}"
    | DuplicatePattern pattern -> $"DuplicatePattern {pattern}"
    | AmbiguousPattern(first, second) -> $"AmbiguousPattern {first} {second}"
    | InvalidPattern(pattern, message) -> $"InvalidPattern {pattern} {message}"

  let formatRouteErrors (errors: RouteTableError list) =
    errors |> List.map formatRouteError |> String.concat "; "

  let formatNavigationError (error: NavigationError) =
    match error with
    | InvalidTarget target -> $"InvalidTarget {target}"
    | RouteNotFound url -> $"RouteNotFound {url}"
    | GuardStopped route -> $"GuardStopped {route}"
    | GuardFailed(route, correlationId) ->
      $"GuardFailed {route} {string correlationId}"
    | RedirectLoop chain ->
      let joined = String.concat ", " chain
      $"RedirectLoop {joined}"
    | ActivationFailed(stage, correlationId) ->
      $"ActivationFailed {stage} {string correlationId}"
    | HistoryFailed message -> $"HistoryFailed {message}"
    | Cancelled -> "Cancelled"
    | Superseded -> "Superseded"

type GooRouter
  (
    routes: RouteDefinition<Blob> seq,
    [<Optional>] ?splash: Func<Blob>,
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
    | Error errors ->
      invalidOp $"Invalid route table: {Helpers.formatRouteErrors errors}"

  interface IRouter<Blob> with
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
  ([<Optional>] ?initialUri: string, [<Optional>] ?logger: ILogger) as this =
  inherit Cell()

  let mutable router: IRouter<Blob> option = None
  let mutable window: Window option = None
  let mutable noContent: Blob option = None
  let mutable unsubscribe: (unit -> unit) option = None
  let mutable started = false
  let mutable pendingInitial = false

  let logNavigationError (uri: string, error: NavigationError) =
    logger
    |> Option.iter(fun log ->
      log.LogError(
        "Failed to navigate to initial URI {Uri}: {Error}",
        uri,
        Helpers.formatNavigationError error
      )
    )

  let runInitial () =
    if not started then
      match router, initialUri with
      | Some current, Some uri ->
        started <- true

        task {
          match! current.Navigate uri CancellationToken.None with
          | Ok _ -> ()
          | Error error -> logNavigationError(uri, error)
        }
        |> ignore
      | _ -> ()

  let tryStartInitial () =
    if not started then
      match router, initialUri, window with
      | Some _, Some _, Some current ->
        pendingInitial <- false
        current.TryPost(fun () -> runInitial()) |> ignore
      | Some _, Some _, None -> pendingInitial <- true
      | _ -> ()

  let requestRebuild () =
    match window with
    | Some current -> current.TryPost(fun () -> this.Rebuild()) |> ignore
    | None -> this.Rebuild()

  member _.Router
    with get () = router
    and set value =
      unsubscribe |> Option.iter(fun dispose -> dispose())
      router <- value

      unsubscribe <-
        value
        |> Option.map(fun current ->
          let handler = fun _ (_: RouterSnapshot<Blob>) -> requestRebuild()

          current.Changed.AddHandler handler
          fun () -> current.Changed.RemoveHandler handler
        )

      tryStartInitial()

  member _.Window
    with get () = window
    and set value =
      window <- value

      if pendingInitial then
        tryStartInitial()

  member _.NoContent
    with get () = noContent
    and set value = noContent <- value

  member _.Start() = runInitial()

  override _.Build() : Blob =
    if pendingInitial then
      tryStartInitial()

    let content =
      router
      |> Option.bind(fun current ->
        current.Snapshot.Content |> ValueOption.toOption
      )

    content
    |> Option.orElse noContent
    |> Option.defaultWith(fun () -> Container() :> Blob)

  interface IDisposable with
    member _.Dispose() =
      unsubscribe |> Option.iter(fun dispose -> dispose())
      unsubscribe <- None
