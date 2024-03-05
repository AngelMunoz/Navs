namespace Navs.Router

open System
open System.Collections.Generic
open System.Threading
open System.Runtime.InteropServices

open FsToolkit.ErrorHandling

open FSharp.Data.Adaptive
open Navs
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser
open UrlTemplates.UrlTemplate

[<Struct; NoComparison>]
type ActiveRouteParams = {
  SegmentIndex: int
  ParamName: string
  ParamValue: string
}

[<Struct; NoComparison; NoEquality>]
type NavigationError<'View> =
  | NavigationCancelled
  | RouteNotFound of url: string
  | CantDeactivate of deactivateGuard: RouteDefinition<'View>
  | CantActivate of activateGuard: RouteDefinition<'View>


module Result =
  let inline requireValueSome (msg: string) (value: 'a voption) =
    match value with
    | ValueSome a -> Ok a
    | ValueNone -> Error msg

module RouteInfo =

  let getParamDiff (urlInfo: UrlInfo) (tplInfo: UrlTemplate) =
    if urlInfo.Segments.Length <> tplInfo.Segments.Length then
      []
    else
      urlInfo.Segments
      |> List.zip tplInfo.Segments
      |> List.indexed
      |> List.choose(fun (index, (segment, urlSegment)) ->
        match segment with
        | ParamSegment(name, _) ->
          Some(
            {
              SegmentIndex = index
              ParamName = name
              ParamValue = urlSegment
            }
          )
        | _ -> None
      )


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


  let getActiveRouteInfo (routes: RouteTrack<'View> seq) (url: string) = result {
    let! activeGraph, routeContext =
      voption {
        let! track, matchInfo =
          routes
          |> Seq.tryPick(fun route ->
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
            | Error whytho -> None
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
      canActivate = [
        while canActivate.Count > 0 do
          canActivate.Pop()
      ]
      canDeactivate = [
        while canDeactivate.Count > 0 do
          canDeactivate.Dequeue()
      ]
    |}

module Router =

  let runGuards<'View>
    (onFalsePredicate: RouteDefinition<'View> -> NavigationError<'View>)
    (guards: (RouteGuard * RouteDefinition<'View>) list)
    nextContext
    =
    async {
      let! token = Async.CancellationToken

      return!
        guards
        |> List.traverseAsyncResultM(fun (guard, definition) ->
          if token.IsCancellationRequested then
            AsyncResult.error NavigationCancelled
          else
            guard.Invoke(nextContext, token)
            |> Async.AwaitTask
            |> AsyncResult.requireTrue(onFalsePredicate definition)
        )
        |> AsyncResult.ignore
    }

  let resolveViewNonCached routeHit nextContext token =
    routeHit.Definition.GetContent.Invoke(nextContext, token) |> Async.AwaitTask


  let navigate
    (
      routes: RouteTrack<'View> seq,
      notFound: (Func<'View>) option,
      content: cval<_>,
      liveNodes: cmap<string, ActiveRouteParams list * _>
    ) =
    fun url -> asyncResult {
      let! token = Async.CancellationToken

      let! activeRouteNodes, currentParams, routeContext =
        RouteInfo.getActiveRouteInfo routes url
        |> Result.mapError(fun _ -> RouteNotFound url)

      let nextContext: RouteContext = {
        Route = url
        UrlInfo = routeContext.UrlInfo
        UrlMatch = routeContext.UrlMatch
      }

      let guards = RouteInfo.extractGuards activeRouteNodes

      do! runGuards CantDeactivate guards.canDeactivate nextContext

      do! runGuards CantActivate guards.canActivate nextContext


      if token.IsCancellationRequested then
        return! Error NavigationCancelled
      else
        // The first route is the currently active route
        // The rest of the routes are the matched "parent" routes.
        match activeRouteNodes |> Seq.tryHead with
        | None ->
          // no templated url found.
          transact(fun _ ->
            content.Value <-
              notFound
              |> ValueOption.ofOption
              |> ValueOption.map(fun f -> f.Invoke())
          )

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
    routes: RouteTrack<'View> seq,
    [<Optional>] ?splash: Func<'View>,
    [<Optional>] ?notFound: Func<'View>,
    [<Optional>] ?historyManager: IHistoryManager<RouteTrack<'View>>
  ) =

  let history: IHistoryManager<RouteTrack<'View>> =
    defaultArg historyManager (HistoryManager())

  let liveNodes = cmap<string, ActiveRouteParams list * 'View>()

  let content =
    cval(splash |> ValueOption.ofOption |> ValueOption.map(fun f -> f.Invoke()))

  let navigate = Router.navigate(routes, notFound, content, liveNodes)

  member _.Content: 'View voption IObservable =
    { new IObservable<'View voption> with
        member _.Subscribe(observer) = content.AddCallback(observer.OnNext)
    }

  member _.AdaptiveContent: 'View voption aval = content

  member _.Navigate
    (
      url: string,
      [<Optional>] ?cancellationToken: CancellationToken
    ) =
    let work = async {
      let! result = navigate url

      match result with
      | Ok tracked ->
        history.SetCurrent(tracked)
        return Ok()
      | Error e -> return Error e
    }

    Async.StartAsTask(work, ?cancellationToken = cancellationToken)

  member _.NavigateByName
    (
      routeName: string,
      [<Optional>] ?routeParams: IReadOnlyDictionary<string, obj>,
      [<Optional>] ?cancellationToken: CancellationToken
    ) =
    let work = async {

      let routeParams: IReadOnlyDictionary<string, obj> =
        routeParams
        // Guess what! double check for NRTs that may come from dotnet langs/types
        |> Option.map(fun p -> p |> Option.ofNull)
        |> Option.flatten
        |> Option.defaultWith(fun _ -> Dictionary())


      match
        routes |> Seq.tryFind(fun route -> route.Definition.Name = routeName)
      with
      | Some route ->
        match UrlTemplate.toUrl route.PatternPath routeParams with
        | Ok url ->
          let! result = navigate url

          match result with
          | Ok tracked ->
            history.SetCurrent(tracked)
            return Ok()
          | Error e -> return Error e
        | Error e -> return Error(RouteNotFound(String.concat ", " e))
      | None -> return Error(RouteNotFound routeName)
    }

    Async.StartAsTask(work, ?cancellationToken = cancellationToken)
