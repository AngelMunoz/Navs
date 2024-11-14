namespace Navs.Router

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading

open FSharp.Data.Adaptive
open IcedTasks
open FsToolkit.ErrorHandling

open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser
open UrlTemplates.UrlTemplate

open Navs

[<Struct; NoComparison>]
type ActiveRouteParams = {
  SegmentIndex: int
  ParamName: string
  ParamValue: string
}

type RouteDisposables() =
  let disposables = ResizeArray<IDisposable>()

  interface IDisposableBag with
    member _.AddDisposable(disposable: IDisposable) =
      disposables.Add(disposable)

    member _.Dispose() : unit =
      for disposable in disposables do
        try
          disposable.Dispose()
        with _ ->
          ()

module RouteInfo =

  [<NoComparison; NoEquality>]
  type RouteUnit<'View> = {
    definition: RouteDefinition<'View>
    activeParams: ActiveRouteParams list
    context: RouteContext
  }

  type Redirect = { from: string; target: string }

  let getParamDiff (urlInfo: UrlInfo) (tplInfo: UrlTemplate) =
    if urlInfo.Segments.Length <> tplInfo.Segments.Length then
      []
    else
      List.zip tplInfo.Segments urlInfo.Segments
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

  let getActiveRouteInfo (routes: RouteDefinition<'View> list) (url: string) = result {
    let! activeRoute, routeContext =
      routes
      |> List.tryPick(fun route ->
        result {
          let! template, urlInfo, matchInfo =
            RouteMatcher.matchStrings route.pattern url

          return
            (route,
             {|
               Route = url
               UrlInfo = urlInfo
               UrlMatch = matchInfo
               UrlTemplate = template
             |})
        }
        |> Result.toOption
      )
      |> Result.requireSome "No matching route found"

    let urlParam = getParamDiff routeContext.UrlInfo routeContext.UrlTemplate

    return {
      definition = activeRoute
      activeParams = urlParam
      context = {
        path = url
        urlInfo = routeContext.UrlInfo
        urlMatch = routeContext.UrlMatch
        disposables = new RouteDisposables()
      }
    }
  }

module Navigable =
  open System.Collections.Immutable
  open RouteInfo

  [<NoComparison; NoEquality>]
  type RouteEnvironment<'View> = {
    routes: RouteDefinition<'View> list
    state: NavigationState
    cache: IDictionary<string, RouteUnit<'View> * 'View>
    activeRoute: RouteUnit<'View> voption
  }

  let navigate
    url
    (env: RouteEnvironment<'View>)
    (nav: INavigable<'View>)
    (stack: Redirect voption)
    (token: CancellationToken)
    =
    taskResult {

      // if we have this in cache, let's jumpthe dance
      match env.cache.TryGetValue url with
      | true, value -> return value
      // otherwise let's try to resolve the issue
      | false, _ ->
        // 1. resolve the route
        if token.IsCancellationRequested then
          return! Error(NavigationCancelled)
        else
          let! nextRoute =
            RouteInfo.getActiveRouteInfo env.routes url
            |> Result.mapError(fun _ -> RouteNotFound url)

          // 2. check deactivation guards

          match env.activeRoute with
          | ValueSome active ->

            do!
              active.definition.canDeactivate
              |> List.traverseTaskResultM(fun guard -> taskResult {
                if token.IsCancellationRequested then
                  return! Error(NavigationCancelled)
                else
                  match!
                    guard.Invoke
                      (ValueSome active.context, nextRoute.context)
                      token
                  with
                  | Continue -> return ()
                  | Redirect redirect ->
                    match stack with
                    | ValueSome { from = from; target = target } ->
                      if from <> url && redirect = target then
                        return ()
                      else
                        return! Error(GuardRedirect redirect)
                    | ValueNone -> return! Error(GuardRedirect redirect)
                  | Stop ->
                    return! Error(CantDeactivate active.definition.pattern)
              })
              |> TaskResult.ignore

            // 2. Start deactivating the current route

            match active.definition.cacheStrategy with
            | NoCache -> active.context.disposables.Dispose()
            | Cache -> ()
            // 2.1. Check Next Route Activation Guards with active route
            do!
              nextRoute.definition.canActivate
              |> List.traverseTaskResultM(fun guard -> taskResult {
                if token.IsCancellationRequested then
                  return! Error(NavigationCancelled)
                else
                  match!
                    guard.Invoke
                      (ValueSome active.context, nextRoute.context)
                      token
                  with
                  | Continue -> return ()
                  | Redirect url -> return! Error(GuardRedirect url)
                  | Stop ->
                    return! Error(CantActivate nextRoute.definition.pattern)
              })
              |> TaskResult.ignore
          | ValueNone ->
            // 2.1 Check Next Route Activation Guards without active route
            do!
              nextRoute.definition.canActivate
              |> List.traverseTaskResultM(fun guard -> taskResult {
                if token.IsCancellationRequested then
                  return! Error(NavigationCancelled)
                else
                  match! guard.Invoke (ValueNone, nextRoute.context) token with
                  | Continue -> return ()
                  | Redirect url -> return! Error(GuardRedirect url)
                  | Stop ->
                    return! Error(CantActivate nextRoute.definition.pattern)
              })
              |> TaskResult.ignore

          if token.IsCancellationRequested then
            return! Error(NavigationCancelled)
          else
            // 3. Resolve the view content
            match nextRoute.definition.cacheStrategy with
            | NoCache ->
              // 3.1 always resolve the content for no-cache

              let! resolved =
                nextRoute.definition.getContent.Invoke
                  (nextRoute.context, nav)
                  token

              return nextRoute, resolved
            | Cache ->
              // 3.2 If we're here, it means this url is not in the cache and we need to resolve it

              let! resolved =
                nextRoute.definition.getContent.Invoke
                  (nextRoute.context, nav)
                  token

              match env.cache.TryAdd(url, (nextRoute, resolved)) with
              | true -> () // Yeah we're good
              | false ->
                // Why though?
                ()

              return nextRoute, resolved
    }

  let (|IsNavigationCancelled|_|) (error: NavigationError<_>) =
    error.IsNavigationCancelled

  [<return: Struct>]
  let (|IsRedirection|_|) (error: NavigationError<_>) =
    match error with
    | GuardRedirect route -> ValueSome route
    | _ -> ValueNone

  let attemptRedirect (env, nav) origin target = cancellableTask {
    let mutable lastResult = ValueNone
    let mutable lastError = ValueNone
    let mutable lastRedirect = ValueNone
    let redirectStack = Stack<RouteInfo.Redirect>()
    redirectStack.Push({ from = origin; target = target })
    let! token = CancellableTask.getCancellationToken()

    while redirectStack.Count > 0 do
      let redirect = redirectStack.Pop()
      lastRedirect <- ValueSome redirect
      let! result = navigate redirect.target env nav (ValueSome redirect) token

      match result with
      | Error(IsRedirection route) ->
        redirectStack.Push(
          {
            from = redirect.target
            target = route
          }
        )

        lastError <- ValueNone
      | Error errors ->
        lastError <- ValueSome errors
        redirectStack.Clear()
      | Ok result -> lastResult <- ValueSome result

    if lastError.IsSome then
      return Error lastError.Value
    else
      match lastResult with
      | ValueNone ->
        match lastRedirect with
        | ValueSome { from = origin; target = target } ->
          return
            Error(
              NavigationFailed
                $"Unable to resolve route, last redirect attempt from %s{origin} to %s{target}"
            )
        | ValueNone -> return Error(NavigationFailed "Unable to resolve route")
      | ValueSome result -> return Ok result
  }

[<Sealed; Class>]
type Router =

  [<CompiledName "Build">]
  static member build<'View>
    (routes: RouteDefinition<'View> seq, [<Optional>] ?splash: unit -> 'View) =
    let routes = routes |> Seq.toList
    let state = cval Idle
    let cache = Dictionary<string, RouteInfo.RouteUnit<'View> * 'View>()
    let activeRoute: RouteInfo.RouteUnit<'View> voption cval = cval ValueNone

    let activeView =
      cval(splash |> Option.map(fun f -> f()) |> ValueOption.ofOption)

    { new IRouter<'View> with
        member _.State = state :> aval<NavigationState>
        member _.StateSnapshot = state |> AVal.force

        member this.Navigate(url, ?cancellationToken) = taskResult {
          let token = defaultArg cancellationToken CancellationToken.None

          use _ =
            token.Register(fun _ -> transact(fun _ -> state.Value <- Idle))

          transact(fun _ -> state.Value <- Navigating)

          let env: Navigable.RouteEnvironment<'View> = {
            routes = routes
            cache = cache
            state = state |> AVal.force
            activeRoute = activeRoute |> AVal.force
          }

          let nav = this :> INavigable<'View>

          let attemptRedirect = Navigable.attemptRedirect(env, nav)

          if token.IsCancellationRequested then
            return! Error(NavigationCancelled)

          try
            let! resolved = task {
              let! result = Navigable.navigate url env nav ValueNone token

              match result with
              | Ok value -> return Ok value
              | Error(Navigable.IsRedirection route) ->
                return! attemptRedirect url route token
              | Error error -> return Error error
            }

            if token.IsCancellationRequested then
              return! Error(NavigationCancelled)

            let route, view = resolved

            transact(fun _ ->
              activeRoute.Value <- ValueSome route
              activeView.Value <- ValueSome view
            )

            return ()
          with
          | :? Tasks.TaskCanceledException ->
            transact(fun _ -> state.Value <- Idle)
            return! Error NavigationCancelled
          | err ->
            transact(fun _ -> state.Value <- Idle)
            return! Error(NavigationFailed err.Message)
        }

        member this.NavigateByName
          (routeName, ?routeParams, ?cancellationToken)
          =
          taskResult {

            let token = defaultArg cancellationToken CancellationToken.None

            let! foundRoute =
              routes
              |> Seq.tryFind(fun r -> r.name = routeName)
              |> Result.requireSome(RouteNotFound routeName)

            let! url =
              result {
                match routeParams with
                | Some p ->
                  let! url = UrlTemplate.toUrl foundRoute.pattern p
                  return url
                | None ->
                  return!
                    UrlTemplate.toUrl foundRoute.pattern (Dictionary<_, _>())
              }
              |> Result.mapError(fun errors ->
                NavigationFailed(errors |> String.concat ", ")
              )

            if token.IsCancellationRequested then
              return! Error(NavigationCancelled)
            else
              return! this.Navigate(url, token)
          }

        member _.Route =
          activeRoute
          |> AVal.map(fun v -> v |> ValueOption.map(fun v -> v.context))

        member _.RouteSnapshot =
          activeRoute |> AVal.force |> ValueOption.map(fun v -> v.context)

        member _.Content = activeView
        member _.ContentSnapshot = activeView |> AVal.force
    }
