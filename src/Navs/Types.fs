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
  }

  [<NoComparison; NoEquality>]
  type RouteTrack<'View> = {
    PatternPath: string
    Definition: RouteDefinition<'View>
    ParentTrack: RouteTrack<'View> voption
  }

  module RouteTrack =
    open FSharp.Data.Adaptive

    let ofDefinitions (routes: RouteDefinition<'View> list) =

      let rec loop currentPattern parent parentTrack children bag =
        let pattern =
          match parent with
          | ValueSome parent -> $"{currentPattern}/{parent.Pattern}"
          | ValueNone -> currentPattern

        children
        |> List.fold
          (fun bag child ->
            let childPattern = $"{pattern}/{child.Pattern}"

            let track = {
              PatternPath = childPattern
              Definition = child
              ParentTrack = parentTrack
            }

            loop
              pattern
              (ValueSome child)
              (ValueSome track)
              child.Children
              (track :: bag)
          )
          bag


      loop "" ValueNone ValueNone routes List.empty

    let flatReverse (track: RouteTrack<'View>) =
      let rec loop (track: RouteTrack<'View>) bag =
        match track.ParentTrack with
        | ValueSome parent -> loop parent (track :: bag)
        | ValueNone -> track :: bag

      loop track List.empty


  // let ofList (routes: RouteDefinition<'View> list) =

  //   // create a map contatenating the paths of the children routes
  //   let map = Dictionary<string, RouteTrack<'View>>()

  //   let rec loop parent children =
  //     let parentPath =
  //       match parent with
  //       | ValueSome parent -> parent.PatternPath
  //       | ValueNone -> ""

  //     for child in children do
  //       let path = sprintf "%s/%s" parentPath child.Pattern

  //       let track = {
  //         PatternPath = path
  //         Definition = child
  //         ParentDefinition = parent
  //       }

  //       map.Add(path, track)

  //       loop (ValueSome track) child.Children

  //   loop ValueNone routes
  //   map

  module Route =

    let inline define (name, path, view) = {
      Name = name
      Pattern = path
      GetContent = Content view
      Children = []
      CanActivate = []
      CanDeactivate = []
    }

    let inline defineResolve (name, path, [<InlineIfLambda>] getContent) = {
      Name = name
      Pattern = path
      GetContent = Resolve getContent
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
