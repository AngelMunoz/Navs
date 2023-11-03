namespace Navs

open System
open System.Collections.Generic
open System.Threading

open FSharp.Control.Reactive
open System.Reactive.Subjects

open UrlTemplates.RouteMatcher
open UrlTemplates.UrlTemplate
open UrlTemplates.UrlParser

open IcedTasks
open FsToolkit.ErrorHandling

type RouteContext = {
  Route: string
  UrlMatch: UrlMatch
  UrlInfo: UrlInfo
}

type RouteGuard = RouteContext -> CancellableValueTask<bool>
type GetView<'View> = RouteContext -> CancellableValueTask<'View>

type RouteDefinition<'View> = {
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

module Mapper =

  let inline mapDefinitionToDefined (definition: RouteDefinition<'View>) = result {
    let! template = UrlTemplate.ofString definition.Path

    return {
      RouteTemplate = template
      Definition = definition
    }
  }

  let mapDefinitions (definitions: RouteDefinition<'View> list) =
    definitions |> List.traverseResultM mapDefinitionToDefined


module Navigation =

  let tryMatchUrl
    (route: string)
    (parsedRoute: UrlInfo)
    (routes: DefinedRoute<'View> list)
    =
    routes
    |> List.tryPick(fun definition ->
      match
        RouteMatcher.matchTemplate definition.RouteTemplate parsedRoute
      with
      | Ok urlMatch ->
        Some(
          {
            Route = route
            UrlMatch = urlMatch
            UrlInfo = parsedRoute
          },
          definition
        )
      | Error _ -> None
    )
    |> Result.requireSome(NotFound route)

  let runGuards (ctx: RouteContext) (guards: RouteGuard list) = cancellableValueTask {
    let! token = CancellableValueTask.getCancellationToken()

    let! op =
      guards
      |> List.traverseTaskResultM(fun guard ->
        let op = cancellableTaskResult {
          let! canActivate = guard ctx

          return! canActivate |> Result.requireTrue FailedToActivate
        }

        op token
      )

    match op with
    | Ok _ -> return Ok()
    | Error e -> return Error e
  }

type HistoryManager<'RouteEntry>(?historySize: int) =
  let historySize = defaultArg historySize 10

  let history = LinkedList<'RouteEntry>()
  let forwardHistory = LinkedList<'RouteEntry>()

  member val Current =
    history.Last
    |> ValueOption.ofNull
    |> ValueOption.map(fun value -> value.Value) with get

  member val CanGoBack = history.Count > 1 with get
  member val CanGoForward = forwardHistory.Count > 0 with get

  member _.SetCurrent(route: 'RouteEntry) =
    if history.Count >= historySize then
      history.RemoveFirst() |> ignore

    history.AddLast(route) |> ignore
    forwardHistory.Clear()

  member _.Next() =
    if forwardHistory.Count <= 0 then
      ValueNone
    else
      let next = forwardHistory.Last.Value
      history.AddLast(next) |> ignore
      forwardHistory.RemoveLast()

      if history.Count >= historySize then
        history.RemoveFirst()

      ValueSome next

  member _.Previous() =
    if history.Count <= 0 then
      ValueNone
    else
      let previous = history.Last.Value
      forwardHistory.AddLast(previous) |> ignore
      history.RemoveLast()

      ValueSome previous


type Router<'View>(routes: DefinedRoute<'View> list) =
  let history = HistoryManager<DefinedRoute<'View> * RouteContext>()

  let route = Subject.behavior routes.Head

  let view: Subject<'View> = Subject.broadcast

  member val ViewContent: IObservable<'View> = view with get

  member val CurrentRoute: IObservable<DefinedRoute<'View>> = route with get

  member val CurrentRouteSnapshot: DefinedRoute<'View> = route.Value with get


  member _.Navigate(path: string, ?cancellationToken: CancellationToken) =
    let token = defaultArg cancellationToken CancellationToken.None

    (cancellableTaskResult {
      let! token = CancellableValueTask.getCancellationToken()

      let current = route.Value

      let! parsedNext =
        UrlParser.ofString path |> Result.mapError InvalidRouteFormat

      let! (nextCtx, nextDef) = Navigation.tryMatchUrl path parsedNext routes

      // check can deactivate
      do!
        (current.Definition.CanDeactivate |> Navigation.runGuards nextCtx) token

      do!
        (current.Definition.CanDeactivate |> Navigation.runGuards nextCtx) token
      // get view
      let! nextView = vTask {
        try
          let! view = nextDef.Definition.View nextCtx token

          return Ok view
        with e ->
          return Error(ViewActivationError e)
      }

      view.OnNext nextView

      history.SetCurrent(nextDef, nextCtx)
      route.OnNext nextDef

      return ()

    })
      token

  member _.Back(?cancellationToken: CancellationToken) =
    let token = defaultArg cancellationToken CancellationToken.None

    (cancellableTaskResult {
      let! token = CancellableValueTask.getCancellationToken()
      do! history.CanGoBack |> Result.requireTrue FailedToMoveBack

      let! current, _ =
        history.Current
        |> Option.ofValueOption
        |> Result.requireSome FailedToMoveBack

      let! nextDef, nextContext =
        history.Previous()
        |> Option.ofValueOption
        |> Result.requireSome FailedToMoveBack

      // check can deactivate
      do!
        (current.Definition.CanDeactivate |> Navigation.runGuards nextContext)
          token

      do!
        (nextDef.Definition.CanDeactivate |> Navigation.runGuards nextContext)
          token
      // get view
      let! previousView = vTask {
        try
          let! view = nextDef.Definition.View nextContext token

          return Ok view
        with e ->
          return Error(ViewActivationError e)
      }

      view.OnNext previousView

      route.OnNext nextDef


      return ()

    })
      token

  member _.Forward(?cancellationToken: CancellationToken) =
    let token = defaultArg cancellationToken CancellationToken.None

    (cancellableTaskResult {
      let! token = CancellableValueTask.getCancellationToken()
      do! history.CanGoForward |> Result.requireTrue FailedToMoveForward

      let! current, _ =
        history.Current
        |> Option.ofValueOption
        |> Result.requireSome FailedToMoveForward

      let! nextDef, nextContext =
        history.Next()
        |> Option.ofValueOption
        |> Result.requireSome FailedToMoveForward

      // check can deactivate
      do!
        (current.Definition.CanDeactivate |> Navigation.runGuards nextContext)
          token

      do!
        (nextDef.Definition.CanDeactivate |> Navigation.runGuards nextContext)
          token
      // get view
      let! previousView = vTask {
        try
          let! view = nextDef.Definition.View nextContext token

          return Ok view
        with e ->
          return Error(ViewActivationError e)
      }

      view.OnNext previousView

      route.OnNext nextDef
      return ()
    })
      token

[<RequireQualifiedAccess>]
module Route =

  type DefinitionPart<'View> =
    | Path of string
    | View of GetView<'View>
    | CanActivate of RouteGuard list
    | CanDeactivate of RouteGuard list

  type RouteDefinitionError =
    | MissingPath
    | MissingView
    | InvalidRouteFormat of string

  let route<'View> () : DefinitionPart<'View> list = [
    CanActivate []
    CanDeactivate []
  ]

  let inline path (path: string) (routeInfo: DefinitionPart<_> list) =
    (Path path) :: routeInfo

  let inline view (view: GetView<'View>) (routeInfo: DefinitionPart<_> list) =
    (View view) :: routeInfo

  let inline canActivate
    (guard: RouteGuard)
    (routeInfo: DefinitionPart<_> list)
    =
    routeInfo
    |> List.map(fun part ->
      match part with
      | CanActivate guards -> CanActivate(guard :: guards)
      | _ -> part
    )

  let inline canDeactivate
    (guard: RouteGuard)
    (routeInfo: DefinitionPart<_> list)
    =
    routeInfo
    |> List.map(fun part ->
      match part with
      | CanDeactivate guards -> CanDeactivate(guard :: guards)
      | _ -> part
    )

  let define (routeInfo: DefinitionPart<_> list) = result {
    let! path =
      routeInfo
      |> List.tryPick(fun part ->
        match part with
        | Path path -> Some path
        | _ -> None
      )
      |> Result.requireSome MissingPath

    do!
      UrlTemplate.ofString path
      |> Result.mapError InvalidRouteFormat
      |> Result.ignore

    let! view =
      routeInfo
      |> List.tryPick(fun part ->
        match part with
        | View view -> Some view
        | _ -> None
      )
      |> Result.requireSome MissingView

    let canActivate =
      routeInfo
      |> List.pick(fun part ->
        match part with
        | CanActivate guards -> Some guards
        | _ -> Some []
      )

    let canDeactivate =
      routeInfo
      |> List.pick(fun part ->
        match part with
        | CanDeactivate guards -> Some guards
        | _ -> Some []
      )

    return {
      Path = path
      View = view
      CanActivate = canActivate
      CanDeactivate = canDeactivate
    }
  }
