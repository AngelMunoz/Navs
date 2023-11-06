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


module RouteInfo =

  [<Struct>]
  type ActiveRouteParams = {
    SegmentIndex: int
    ParamName: string
    ParamValue: string
  }

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

type Router<'View>(routes: RouteTrack<'View> list) =

  let content = cval ValueNone
  member _.Content: 'View voption aval = content

  member _.Navigate(url: string, ?cancellationToken: CancellationToken) = taskResult {
    let token = defaultArg cancellationToken CancellationToken.None

    let! activeGraph, urlParam, routeContext =
      RouteInfo.getActiveRouteInfo routes url

    let nextContext: RouteContext = {
      Route = url
      UrlInfo = routeContext.UrlInfo
      UrlMatch = routeContext.UrlMatch
    }

    let guards = RouteInfo.extractGuards activeGraph

    do!
      guards.canDeactivate
      |> List.traverseTaskResultM(fun guard ->
        (guard nextContext token).AsTask()
        |> TaskResult.requireTrue "Can't deactivate"
      )
      |> TaskResult.ignore

    do!
      guards.canActivate
      |> List.traverseTaskResultM(fun guard ->
        (guard nextContext token).AsTask()
        |> TaskResult.requireTrue "Can't activate"
      )
      |> TaskResult.ignore

    match activeGraph |> List.tryLast with
    | Some last ->
      match last.Definition.GetContent with
      | Resolve resolve ->
        let! view = resolve nextContext token
        transact(fun _ -> content.Value <- (ValueSome view))
        return ()
      | Content view ->
        transact(fun _ -> content.Value <- (ValueSome view))
        return ()

    | None ->
      // TODO: Set NotFound content here

      return ()
  }
