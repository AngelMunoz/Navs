namespace Navs.Router

open System
open System.Collections.Generic
open System.Threading
open System.Runtime.InteropServices

open FsToolkit.ErrorHandling

open FSharp.Data.Adaptive

open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser
open UrlTemplates.UrlTemplate

open Navs
open System.Threading.Tasks

[<Struct; NoComparison>]
type ActiveRouteParams = {
  SegmentIndex: int
  ParamName: string
  ParamValue: string
}


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
    |> Seq.iter(fun route ->
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

  let resolveViewNonCached router routeHit nextContext = async {
    let! token = Async.CancellationToken

    if token.IsCancellationRequested then
      return Error NavigationCancelled
    else
      let! result =
        routeHit.Definition.GetContent.Invoke(nextContext, router, token)
        |> Async.AwaitTask
        |> Async.Catch

      match result with
      | Choice1Of2 view -> return Ok view
      | Choice2Of2 ex -> return Error NavigationError.NavigationCancelled
  }


  let navigate
    (
      router: INavigate<_>,
      routes: RouteTrack<'View> seq,
      notFound: (Func<INavigate<_>, 'View>) option,
      content: cval<_>,
      liveNodes: cmap<string, ActiveRouteParams list * _>,
      activeRoute:
        cval<(RouteContext * (RouteGuard * RouteDefinition<_>) list) voption>
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

      match activeRoute |> AVal.force with
      | ValueSome(activeContext, activeGuards) ->

        do! runGuards CantDeactivate activeGuards activeContext

      | ValueNone -> ()


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
            activeRoute.Value <- ValueNone
            content.Value <- notFound |> Option.map(fun f -> f.Invoke(router))
          )

          return! Error(RouteNotFound url)
        | Some routeHit ->

          match routeHit.Definition.CacheStrategy with
          | NoCache -> // No caching, just resolve any time.

            let! view = resolveViewNonCached router routeHit nextContext

            transact(fun _ ->
              activeRoute.Value <- ValueSome(nextContext, guards.canDeactivate)
              content.Value <- Some view
            )
          | Cache ->
            // Templated url hit, check the cache and update if necessary
            match liveNodes.TryGetValue routeHit.PatternPath with
            | Some(oldParams, oldView) ->
              // we've visited this route template before
              if currentParams = oldParams then
                transact(fun _ ->
                  activeRoute.Value <-
                    ValueSome(nextContext, guards.canDeactivate)

                  content.Value <- (Some oldView)
                )
              else
                let! view = resolveViewNonCached router routeHit nextContext

                transact(fun _ ->
                  activeRoute.Value <-
                    ValueSome(nextContext, guards.canDeactivate)

                  liveNodes.Item(routeHit.PatternPath) <- (currentParams, view)

                  content.Value <- Some view
                )
            | None ->
              // no cached templated url, resolve the view
              let! view = resolveViewNonCached router routeHit nextContext

              // This is the first time we hit this templated url, add it to the map
              transact(fun _ ->

                activeRoute.Value <-
                  ValueSome(nextContext, guards.canDeactivate)

                liveNodes.Add(routeHit.PatternPath, (currentParams, view))
                |> ignore

                content.Value <- Some view
              )

          return routeHit
    }

type Router<'View>
  (
    routes: RouteTrack<'View> seq,
    [<Optional>] ?splash: Func<INavigate<'View>, 'View>,
    [<Optional>] ?notFound: Func<INavigate<'View>, 'View>,
    [<Optional>] ?historyManager: IHistoryManager<RouteTrack<'View>>
  ) as this =

  let history: IHistoryManager<RouteTrack<'View>> =
    defaultArg historyManager (HistoryManager())

  let liveNodes = cmap<string, ActiveRouteParams list * 'View>()

  let liveRoute = cval(ValueNone)

  let content = cval(splash |> Option.map(fun f -> f.Invoke(this)))

  let navigate =
    Router.navigate(this, routes, notFound, content, liveNodes, liveRoute)

  member _.Content: 'View IObservable =
    { new IObservable<'View> with
        member _.Subscribe(observer) =
          content.AddCallback(fun value ->
            match value with
            | Some view -> observer.OnNext(view)
            | None -> ()
          )
    }

  member _.AdaptiveContent: 'View option aval = content

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
      | Error e ->
        match e with
        | RouteNotFound _ ->
          transact(fun _ ->
            content.Value <- notFound |> Option.map(fun f -> f.Invoke(this))
          )
        | _ -> ()

        return Error e
    }

    task {
      try
        return!
          Async.StartImmediateAsTask(
            work,
            ?cancellationToken = cancellationToken
          )
      with
      | :? TaskCanceledException
      | :? OperationCanceledException -> return Error NavigationCancelled
    }

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

      let setNotFound e =
        match e with
        | RouteNotFound _ ->
          transact(fun _ ->
            content.Value <- notFound |> Option.map(fun f -> f.Invoke(this))
          )
        | _ -> ()

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
          | Error e ->
            setNotFound e

            return Error e
        | Error e ->
          let error = RouteNotFound(String.concat ", " e)
          setNotFound error
          return Error error
      | None ->
        let error = RouteNotFound routeName
        setNotFound error
        return Error error
    }

    task {
      try
        return!
          Async.StartImmediateAsTask(
            work,
            ?cancellationToken = cancellationToken
          )
      with
      | :? TaskCanceledException
      | :? OperationCanceledException -> return Error NavigationCancelled
    }


  interface INavigate<'View> with

    member _.Navigate(name, [<Optional>] ?cancellationToken) =
      this.Navigate(name, ?cancellationToken = cancellationToken)

    member _.NavigateByName
      (
        name,
        [<Optional>] ?routeParams,
        [<Optional>] ?cancellationToken
      ) =
      this.NavigateByName(
        name,
        ?routeParams = routeParams,
        ?cancellationToken = cancellationToken
      )
