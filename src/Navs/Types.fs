namespace Navs

open System.Collections.Generic
open UrlTemplates.RouteMatcher
open UrlTemplates.UrlTemplate
open UrlTemplates.UrlParser

open FsToolkit.ErrorHandling
open IcedTasks

type RouteContext = {
  Route: string
  UrlMatch: UrlMatch
  UrlInfo: UrlInfo
}

type RouteGuard = RouteContext -> CancellableValueTask<bool>
type GetView<'View> = RouteContext -> CancellableValueTask<'View>


[<Struct>]
type CacheStrategy =
  | NoCache
  | Cache

[<Struct>]
type GetContent<'View> =
  | Resolve of resolve: (RouteContext -> CancellableValueTask<'View>)
  | Content of content: 'View

[<NoComparison; NoEquality>]
type RouteDefinition<'View> = {
  Name: string
  Pattern: string
  GetContent: GetContent<'View>
  Children: RouteDefinition<'View> list
  CanActivate: RouteGuard list
  CanDeactivate: RouteGuard list
  CacheStrategy: CacheStrategy
}

[<NoComparison; NoEquality>]
type RouteTrack<'View> = {
  PatternPath: string
  Definition: RouteDefinition<'View>
  ParentTrack: RouteTrack<'View> voption
  Children: RouteTrack<'View> list
}

module RouteTrack =

  let rec processChildren pattern parent children =
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

  let getDefinition
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
      let pattern = $"{currentPattern}/{track.Pattern}"

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

  let ofDefinitions (routes: RouteDefinition<'View> seq) = [
    for route in routes do
      yield! getDefinition "" ValueNone route
  ]
