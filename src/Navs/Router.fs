namespace Navs.Router

open System
open System.Collections.Generic
open System.Threading

open IcedTasks
open FsToolkit.ErrorHandling

open FSharp.Data.Adaptive
open Navs
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser
open UrlTemplates.UrlTemplate


module Result =
  let inline requireValueSome (msg: string) (value: 'a voption) =
    match value with
    | ValueSome a -> Ok a
    | ValueNone -> Error msg

[<Struct>]
type ActiveRouteParams = {
  SegmentIndex: int
  ParamName: string
  ParamValue: string
}

module RouteInfo =


  let getParamDiff (urlInfo: UrlInfo) (tplInfo: UrlTemplate) =
    tplInfo.Segments
    |> List.mapi(fun index segment ->
      result {
        let! urlSegment =
          urlInfo.Segments
          |> List.tryItem index
          |> Result.requireSome "Param segment not found in url"

        match segment with
        | ParamSegment(name, _) ->
          return {
            SegmentIndex = index
            ParamName = name
            ParamValue = urlSegment
          }
        | _ -> return! Error "Not a param segment"
      }
      |> Result.toOption
    )
    |> List.choose id

  let digUpToRoot (track: RouteTrack<'View>) =
    let queue = Queue<RouteTrack<'View>>()
    let result = ResizeArray<RouteTrack<'View>>()

    queue.Enqueue(track)

    while queue.Count > 0 do
      let currentTrack = queue.Dequeue()
      result.Add currentTrack

      match currentTrack.ParentTrack with
      | ValueSome parent -> queue.Enqueue(parent)
      | ValueNone -> ()

    result


  let getActiveRouteInfo (routes: RouteTrack<'View> list) (url: string) = result {
    let! activeGraph, routeContext =
      voption {
        let! track, matchInfo =
          routes
          |> List.tryPick(fun route ->
            match RouteMatcher.matchStrings route.PatternPath url with
            | Ok(template, urlinfo, matchInfo) ->
              Some(
                route,
                {|
                  Route = url
                  UrlInfo = urlinfo
                  UrlMatch = matchInfo
                  UrlTemplate = template
                |}
              )
            | Error _ -> None
          )

        let tracks = digUpToRoot track
        return tracks, matchInfo
      }
      |> Result.requireValueSome "No matching route found"

    let urlParam = getParamDiff routeContext.UrlInfo routeContext.UrlTemplate
    return activeGraph, urlParam, routeContext
  }

  let extractGuards (activeGraph: RouteTrack<'View> seq) =

    let canActivate = Stack<RouteGuard * RouteDefinition<'View>>()
    let canDeactivate = Queue<RouteGuard * RouteDefinition<'View>>()

    activeGraph
    |> Array.ofSeq
    |> Array.Parallel.iter(fun route ->
      for guard in route.Definition.CanActivate do
        canActivate.Push(guard, route.Definition)

      for guard in route.Definition.CanDeactivate do
        canDeactivate.Enqueue(guard, route.Definition)
    )

    struct {|
      canActivate = canActivate |> Seq.toList
      canDeactivate = canDeactivate |> Seq.toList
    |}

[<Struct>]
type NavigationError<'View> =
  | NavigationCancelled
  | RouteNotFound of url: string
  | CantDeactivate of deactivateGuard: RouteDefinition<'View>
  | CantActivate of activateGuard: RouteDefinition<'View>

module Router =

  let runGuards<'View>
    (onFalsePredicate: RouteDefinition<'View> -> NavigationError<'View>)
    (guards: (RouteGuard * RouteDefinition<'View>) list)
    nextContext
    =
    cancellableTask {
      let! token = CancellableValueTask.getCancellationToken()

      return!
        guards
        |> List.traverseTaskResultM(fun (guard, definition) ->
          if token.IsCancellationRequested then
            TaskResult.error NavigationCancelled
          else
            (guard nextContext token).AsTask()
            |> TaskResult.requireTrue(onFalsePredicate definition)
        )
        |> TaskResult.ignore
    }

  let resolveViewNonCached routeHit nextContext = cancellableValueTask {
    match routeHit.Definition.GetContent with
    | Resolve resolve -> return! resolve nextContext
    | Content view -> return view
  }


  let navigate
    (
      routes: RouteTrack<'View> list,
      notFound,
      content: cval<_>,
      liveNodes: cmap<string, ActiveRouteParams list * _>
    )

    =
    fun url token -> taskResult {

      let! activeRouteNodes, currentParams, routeContext =
        RouteInfo.getActiveRouteInfo routes url
        |> Result.mapError(fun _ -> RouteNotFound url)

      let nextContext: RouteContext = {
        Route = url
        UrlInfo = routeContext.UrlInfo
        UrlMatch = routeContext.UrlMatch
      }

      let guards = RouteInfo.extractGuards activeRouteNodes

      do! runGuards CantDeactivate guards.canDeactivate nextContext token

      do! runGuards CantActivate guards.canActivate nextContext token


      if token.IsCancellationRequested then
        return! Error NavigationCancelled
      else
        // The first route is the currently active route
        // The rest of the routes are the matched "parent" routes.
        match activeRouteNodes |> Seq.tryHead with
        | None ->
          // no templated url found.
          transact(fun _ -> content.Value <- notFound |> ValueOption.ofOption)
          return! Error(RouteNotFound url)
        | Some routeHit ->
          match routeHit.Definition.CacheStrategy with
          | NoCache -> // No caching, just resolve any time.
            let! view = resolveViewNonCached routeHit nextContext token

            transact(fun _ -> content.Value <- (ValueSome view))
          | Cache ->
            // Templated url hit, check the cache and update if necessary
            match liveNodes.TryGetValue routeHit.PatternPath with
            | Some(oldParams, oldView) ->
              // we've visited this route template before
              if currentParams = oldParams then
                transact(fun _ -> content.Value <- (ValueSome oldView))
              else
                let! view = resolveViewNonCached routeHit nextContext token

                transact(fun _ ->
                  liveNodes.Item(routeHit.PatternPath) <- (currentParams, view)
                  content.Value <- (ValueSome view)
                )
            | None ->
              // no cached templated url, resolve the view
              let! view = resolveViewNonCached routeHit nextContext token

              // This is the first time we hit this templated url, add it to the map
              transact(fun _ ->
                liveNodes.Add(routeHit.PatternPath, (currentParams, view))
                |> ignore

                content.Value <- (ValueSome view)
              )

          return routeHit
    }


type Router<'View>
  (
    routes: RouteTrack<'View> list,
    ?splash: 'View,
    ?notFound: 'View,
    ?historyManager: IHistoryManager<RouteTrack<'View>>
  ) =

  let history: IHistoryManager<RouteTrack<'View>> =
    defaultArg historyManager (HistoryManager())

  let liveNodes = cmap<string, ActiveRouteParams list * 'View>()
  let content = cval(splash |> ValueOption.ofOption)

  let navigate = Router.navigate(routes, notFound, content, liveNodes)

  member _.Content: 'View voption IObservable =
    { new IObservable<'View voption> with
        member _.Subscribe(observer) = content.AddCallback(observer.OnNext)
    }

  member _.AContent: 'View voption aval = content

  member _.Navigate(url: string, ?cancellationToken: CancellationToken) = task {
    let token = defaultArg cancellationToken CancellationToken.None

    let! result = navigate url token

    match result with
    | Ok tracked ->
      history.SetCurrent(tracked)
      return Ok()
    | Error e -> return Error e
  }

  member _.CanGoBack = history.CanGoBack
  member _.CanGoForward = history.CanGoForward

  member _.Back(?cancellationToken: CancellationToken) = task {
    let token = defaultArg cancellationToken CancellationToken.None

    match history.Previous() with
    | ValueSome tracked ->
      let! result = navigate tracked.PatternPath token

      match result with
      | Ok _ -> return Ok()
      | Error e -> return Error e
    | ValueNone -> return Ok()
  }

  member _.Forward(?cancellationToken: CancellationToken) = task {
    let token = defaultArg cancellationToken CancellationToken.None

    match history.Next() with
    | ValueSome tracked ->
      let! result = navigate tracked.PatternPath token

      match result with
      | Ok _ -> return Ok()
      | Error e -> return Error e
    | ValueNone -> return Ok()
  }
