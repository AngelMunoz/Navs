namespace Navs

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
    Path: string
    GetContent: GetContent<'View>
    Children: RouteDefinition<'View> list
    CanActivate: RouteGuard list
    CanDeactivate: RouteGuard list
  }

  module RouteTree =
    open System.Collections.Generic

    let ofList
      (routes: RouteDefinition<'View> list)
      : Dictionary<string, RouteDefinition<'View>> =
      // create a map contatenating the paths of the children routes
      let map = Dictionary<string, RouteDefinition<'View>>()

      let rec loop parentPath children =
        for route in children do
          let path = sprintf "%s/%s" parentPath route.Path
          map.Add(path, route)
          loop path route.Children

      loop "" routes
      map




  module Route =
    let inline define (path, [<InlineIfLambda>] getContent) = {
      Path = path
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
