namespace Navs.FuncUI

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open Microsoft.Extensions.Logging
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Navs
open Navs.Router

type FuncUIRouter
  (
    routes: RouteDefinition<IView> seq,
    [<Optional>] ?splash: Func<IView>,
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

  interface IRouter<IView> with
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

[<Extension>]
type IComponentContextExtensions =

  [<Extension>]
  static member useRouter(context: IComponentContext, router: IRouter<IView>) =
    let view = context.useState(router.Snapshot.Content)

    context.useEffect(
      handler =
        (fun () ->
          let handler =
            fun _ (_: RouterSnapshot<IView>) ->
              view.Set(router.Snapshot.Content)

          router.Changed.AddHandler handler

          { new IDisposable with
              member _.Dispose() = router.Changed.RemoveHandler handler
          }
        ),
      triggers = [ EffectTrigger.AfterInit ]
    )

    view :> IReadable<IView voption>

[<AutoOpen>]
module DSL =

  [<Class>]
  type RouterOutlet =

    static member create
      (
        router: IRouter<IView>,
        ?initialUri: string,
        ?noContent: IView,
        ?transition: Avalonia.Animation.IPageTransition,
        ?logger: ILogger
      ) =
      let logNavigationError (uri: string) (error: NavigationError) =
        match logger with
        | Some log ->
          log.LogError(
            "Failed to navigate to initial URI {Uri}: {Error}",
            uri,
            error
          )
        | None -> ()

      let initialNavigation () =
        let uri = defaultArg initialUri "/"

        task {
          match! router.Navigate uri CancellationToken.None with
          | Ok _ -> ()
          | Error error -> logNavigationError uri error
        }
        |> ignore

      Component.create(
        "router-outlet",
        fun context ->
          let view = context.useRouter router

          context.useEffect(
            handler = initialNavigation,
            triggers = [ EffectTrigger.AfterInit ]
          )

          let fallback =
            noContent |> Option.defaultValue(ContentControl.create [])

          let content =
            view
            |> State.readMap(fun value ->
              value |> ValueOption.defaultValue fallback
            )

          let pageTransition =
            transition
            |> Option.defaultWith(fun () ->
              Avalonia.Animation.CompositePageTransition(
                PageTransitions =
                  ResizeArray [
                    Avalonia.Animation.CrossFade(
                      TimeSpan.FromMilliseconds 150.
                    )
                    :> Avalonia.Animation.IPageTransition
                    Avalonia.Animation.PageSlide(
                      TimeSpan.FromMilliseconds 300.
                    )
                    :> Avalonia.Animation.IPageTransition
                  ]
              )
            )

          TransitioningContentControl.create [
            TransitioningContentControl.content content.Current
            TransitioningContentControl.pageTransition pageTransition
          ]
      )
