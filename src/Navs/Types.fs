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

type RouteDefinition<'View> = {
  Name: string
  Path: string
  View: GetView<'View>
  CanActivate: RouteGuard list
  CanDeactivate: RouteGuard list
}

type DefinedRoute<'View> = {
  RouteTemplate: UrlTemplate
  Definition: RouteDefinition<'View>
}

type NavigationError =
  | InvalidRouteFormat of string
  | ViewActivationError of exn
  | NotFound of string
  | FailedToDeactivate
  | FailedToActivate
  | FailedToMoveBack
  | FailedToMoveForward

module Experiments =

  type GetContent<'View> = RouteContext -> CancellableValueTask<'View>

  [<NoComparison; NoEquality>]
  type RouteDefinition<'View> = {
    Pattern: string
    GetContent: GetContent<'View>
    Children: RouteDefinition<'View> list
    CanActivate: RouteGuard list
    CanDeactivate: RouteGuard list
  }

  [<NoComparison; NoEquality>]
  type RouteTrack<'View> = {
    PatternPath: string
    Definition: RouteDefinition<'View>
    ParentDefinition: RouteTrack<'View> option
  }

  module RouteTree =

    let ofList (routes: RouteDefinition<'View> list) =
      // create a map contatenating the paths of the children routes
      let map = Dictionary<string, RouteTrack<'View>>()

      let rec loop parent children =
        let parentPath =
          match parent with
          | Some parent -> parent.PatternPath
          | None -> ""

        for child in children do
          let path = sprintf "%s/%s" parentPath child.Pattern

          let track = {
            PatternPath = path
            Definition = child
            ParentDefinition = parent
          }

          map.Add(path, track)

          loop (Some track) child.Children

      loop None routes
      map

  module Route =
    let inline define (path, [<InlineIfLambda>] getContent) = {
      Pattern = path
      GetContent = getContent
      Children = []
      CanActivate = []
      CanDeactivate = []
    }

    let inline child child definition : RouteDefinition<_> = {
      definition with
          Children = child :: definition.Children
    }

    let inline children children definition : RouteDefinition<_> = {
      definition with
          Children = children @ definition.Children
    }

    let inline canActivate
      ([<InlineIfLambda>] guard)
      definition
      : RouteDefinition<_> =
      {
        definition with
            CanActivate = guard :: definition.CanActivate
      }

    let inline canDeactivate
      ([<InlineIfLambda>] guard)
      definition
      : RouteDefinition<_> =
      {
        definition with
            CanDeactivate = guard :: definition.CanDeactivate
      }
