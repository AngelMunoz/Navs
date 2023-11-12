namespace Navs.RouterB

open System
open FSharp.Data.Adaptive

open FsToolkit.ErrorHandling

open Navs
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser
open UrlTemplates.UrlTemplate
open Navs.Experiments
open System.Collections.Generic

open IcedTasks
open System.Threading

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

  let getActiveRouteInfo (routes: RouteTrack<'View> list) (url: string) = result {
    let! activeGraph, routeContext =
      voption {
        let! tracks, matchInfo =
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

        let tracks = tracks |> RouteTrack.flatReverse
        return tracks, matchInfo
      }
      |> Result.requireValueSome "No matching route found"

    let urlParam = getParamDiff routeContext.UrlInfo routeContext.UrlTemplate
    return activeGraph, urlParam, routeContext
  }

  let extractGuards (activeGraph: RouteTrack<'View> list) =
    let guards =
      activeGraph
      |> List.fold
        (fun
             (current:
               struct {|
                 canActivate: _ list
                 canDeactivate: _ list
               |})
             next -> struct {|
          current with
              canActivate = next.Definition.CanActivate @ current.canActivate
              canDeactivate =
                next.Definition.CanDeactivate @ current.canDeactivate
        |})
        (struct {|
          canActivate = []
          canDeactivate = []
        |})

    struct {|
      guards with
          // canDeactivate guards must run in reverse
          // from furthest from root in hierarchy to closest from root
          canDeactivate = guards.canDeactivate |> List.rev
    |}

type NavigationError =
  | RouteNotFound of string
  | CantDeactivate
  | CantActivate

type Router<'View>(routes: RouteTrack<'View> list) =

  let liveNodes = cmap<string, ActiveRouteParams list * 'View>()
  let content = cval ValueNone
  member _.Content: 'View voption aval = content

  member _.Navigate(url: string, ?cancellationToken: CancellationToken) =
    let token = defaultArg cancellationToken CancellationToken.None

    let job = cancellableTaskResult {
      let! token = CancellableTaskResult.getCancellationToken()

      let! activeRouteNodes, currentParams, routeContext =
        RouteInfo.getActiveRouteInfo routes url
        |> Result.mapError(fun _ -> RouteNotFound url)

      let nextContext: RouteContext = {
        Route = url
        UrlInfo = routeContext.UrlInfo
        UrlMatch = routeContext.UrlMatch
      }

      let guards = RouteInfo.extractGuards activeRouteNodes

      do!
        guards.canDeactivate
        |> List.traverseTaskResultM(fun guard ->
          (guard nextContext token).AsTask()
          |> TaskResult.requireTrue CantDeactivate
        )
        |> TaskResult.ignore

      do!
        guards.canActivate
        |> List.traverseTaskResultM(fun guard ->
          (guard nextContext token).AsTask()
          |> TaskResult.requireTrue CantActivate
        )
        |> TaskResult.ignore

      // check the innermost route node as it will have
      // the top level route we need to resolve
      // if the route has a parent we will check
      // in the cache for both the parent and the child
      // if the parent is not cached we will resolve it
      // and cache it, at this point the child will be resolved and cached as well
      // for the current params, next time we navigate to the same route we'll
      // have the parent cached and we can use it to resolve the child unless
      // the params have changed in which case we need to re-resolve both the parent and the child
      match activeRouteNodes |> List.tryLast with
      | Some last ->
        match liveNodes.TryGetValue last.PatternPath with
        | Some(oldParams, oldView) ->
          // TODO: validate route params are the same as the old ones
          // If they are we can use the cached view
          // If they are not we need to re-resolve the view


          // TODO: check if this route has a parent or is a top level view
          // and repeat the process.
          ()
        | None ->
          // resolve the view for this route
          let! view = cancellableValueTask {
            let! token = CancellableValueTask.getCancellationToken()

            match last.Definition.GetContent with
            | Resolve resolve -> return! resolve nextContext token
            | Content view -> return view
          }

          // This is the first time we hit this view, add it to the map
          transact(fun _ ->
            // TODO: ensure that current params are just up to this node, not further
            liveNodes.Add(last.PatternPath, (currentParams, view))
          )
          |> Result.requireTrue "Failed to add view to liveNodes"
          |> Result.teeError(printfn "%s")
          |> Result.ignoreError

          // Check if this view has a parent
          match last.ParentTrack with
          | ValueNone ->
            // No parent this route is the top level route
            transact(fun _ -> content.Value <- (ValueSome view))
            return ()
          | ValueSome parent ->
            // It has a parent let's check if it has been cached
            match liveNodes.TryGetValue parent.PatternPath with
            | Some(oldParentParams, oldParentView) ->
              // TODO: check if the old parent params are the same as the new ones
              // If they are we use the cached view
              // If they are not we need to re-resolve the view


              let! view = cancellableValueTask {
                let! token = CancellableValueTask.getCancellationToken()

                match last.Definition.GetContent with
                | Resolve resolve -> return! resolve nextContext token
                | Content view -> return view
              }

              transact(fun _ ->
                // TODO: ensure that current params are just up to this node, not further
                liveNodes.Add(last.PatternPath, (currentParams, view))
              )
              |> Result.requireTrue "Failed to add view to liveNodes"
              |> Result.teeError(printfn "%s")
              |> Result.ignoreError
              // Update the top level view
              transact(fun _ -> content.Value <- (ValueSome view))
              return ()
            // This view has not parent, it is the top level view we can safely set the content
            | None -> transact(fun _ -> content.Value <- (ValueSome view))
      | None ->
        // TODO: Fail to navigate or just error out before setting No content?
        transact(fun _ -> content.Value <- ValueNone)
        return! Error(RouteNotFound url)
    }

    job token
