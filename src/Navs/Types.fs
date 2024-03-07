namespace Navs

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open System.Collections.Generic
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlParser

type RouteContext = {
  Route: string
  UrlMatch: UrlMatch
  UrlInfo: UrlInfo
}


[<Struct>]
type CacheStrategy =
  | NoCache
  | Cache

type RouteGuard = Func<RouteContext, CancellationToken, Task<bool>>

and GetView<'View> =
  Func<RouteContext, INavigate<'View>, CancellationToken, Task<'View>>

and [<Struct; NoComparison; NoEquality>] NavigationError<'View> =
  | NavigationCancelled
  | RouteNotFound of url: string
  | CantDeactivate of deactivateGuard: RouteDefinition<'View>
  | CantActivate of activateGuard: RouteDefinition<'View>


and [<NoComparison; NoEquality>] RouteDefinition<'View> = {
  Name: string
  Pattern: string
  GetContent: GetView<'View>
  Children: RouteDefinition<'View> list
  CanActivate: RouteGuard list
  CanDeactivate: RouteGuard list
  CacheStrategy: CacheStrategy
}

and INavigate<'View> =

  abstract member Navigate:
    url: string * [<Optional>] ?cancellationToken: CancellationToken ->
      Task<Result<unit, NavigationError<'View>>>

  abstract member NavigateByName:
    routeName: string *
    [<Optional>] ?routeParams: IReadOnlyDictionary<string, obj> *
    [<Optional>] ?cancellationToken: CancellationToken ->
      Task<Result<unit, NavigationError<'View>>>

[<NoComparison; NoEquality>]
type RouteTrack<'View> = {
  PatternPath: string
  Definition: RouteDefinition<'View>
  ParentTrack: RouteTrack<'View> voption
  Children: RouteTrack<'View> list
}

module RouteTracks =

  let rec internal processChildren pattern parent children =
    match children with
    | [] -> []
    | child :: rest ->
      let childTrack = {
        PatternPath = $"{pattern}/{child.Pattern}"
        Definition = child
        ParentTrack = parent
        Children = []
      }

      {
        childTrack with
            Children =
              processChildren
                $"{pattern}/{child.Pattern}"
                (ValueSome childTrack)
                child.Children
      }
      :: processChildren pattern parent rest

  let internal getDefinition
    currentPattern
    (parent: RouteTrack<'View> voption)
    (track: RouteDefinition<'View>)
    =
    let queue =
      Queue<string *
      RouteTrack<'View> voption *
      RouteDefinition<'View> *
      RouteTrack<'View> list>()

    let result = ResizeArray<RouteTrack<'View>>()

    queue.Enqueue(currentPattern, parent, track, [])

    while queue.Count > 0 do
      let currentPattern, parent, track, siblings = queue.Dequeue()

      let pattern =
        if currentPattern = "" then
          track.Pattern
        else if parent.IsSome && currentPattern.EndsWith('/') then
          $"{currentPattern}{track.Pattern}"
        else
          $"{currentPattern}/{track.Pattern}"

      let currentTrack = {
        PatternPath = pattern
        Definition = track
        ParentTrack = parent
        Children = siblings
      }

      result.Add currentTrack

      let childrenTracks =
        processChildren pattern (ValueSome currentTrack) track.Children

      for childTrack in childrenTracks do
        queue.Enqueue(
          pattern,
          ValueSome currentTrack,
          childTrack.Definition,
          childTrack.Children
        )

    result

  [<CompiledName "FromDefinitions">]
  let fromDefinitions (routes: RouteDefinition<'View> seq) = [
    for route in routes do
      yield! getDefinition "" ValueNone route
  ]
