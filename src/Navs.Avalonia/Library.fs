namespace Navs.Avalonia

open System
open System.Runtime.InteropServices
open System.Threading
open Avalonia.Animation
open Avalonia.Controls
open Microsoft.Extensions.Logging
open Navs
open Navs.Router

type AvaloniaRouter
  (
    routes: RouteDefinition<Control> seq,
    [<Optional>] ?splash: Func<Control>,
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

  interface IRouter<Control> with
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

type RouterOutlet() as this =
  inherit UserControl()

  let content =
    TransitioningContentControl(
      PageTransition = CrossFade(TimeSpan.FromMilliseconds 150.)
    )

  let mutable router: IRouter<Control> option = None
  let mutable currentContent: Control option = None
  let mutable unsubscribe: (unit -> unit) option = None

  let update () =
    let activeContent =
      router |> Option.map(fun currentRouter -> currentRouter.Snapshot.Content)

    match activeContent with
    | None -> content.Content <- null
    | Some(ValueSome view) -> content.Content <- view
    | Some ValueNone ->
      content.Content <-
        currentContent
        |> Option.map(fun fallback -> fallback :> obj)
        |> Option.toObj

  do this.Content <- content

  member this.Router
    with get () = router
    and set value =
      unsubscribe |> Option.iter(fun dispose -> dispose())
      router <- value

      unsubscribe <-
        value
        |> Option.map(fun current ->
          let handler = fun _ (_: RouterSnapshot<Control>) -> update()

          current.Changed.AddHandler handler
          fun () -> current.Changed.RemoveHandler handler
        )

      update()

  member this.PageTransition
    with get () = content.PageTransition
    and set value = content.PageTransition <- value

  member this.NoContent
    with get () = currentContent
    and set value =
      currentContent <- value
      update()
