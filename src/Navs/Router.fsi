namespace Navs.Router

open System
open System.Collections.Generic
open System.Threading
open System.Runtime.InteropServices

open FSharp.Data.Adaptive
open Navs

[<Struct>]
type ActiveRouteParams =
  { SegmentIndex: int
    ParamName: string
    ParamValue: string }

[<NoComparison>]
[<Struct>]
type NavigationError<'View> =
  | NavigationCancelled
  | RouteNotFound of url: string
  | CantDeactivate of deactivateGuard: RouteDefinition<'View>
  | CantActivate of activateGuard: RouteDefinition<'View>

type Router<'View> =
  new:
    routes: RouteTrack<'View> seq *
    [<Optional>] ?splash: 'View *
    [<Optional>] ?notFound: 'View *
    [<Optional>] ?historyManager: IHistoryManager<RouteTrack<'View>> ->
      Router<'View>

  member Content: IObservable<'View voption>
  member AContent: aval<'View voption>

  member Navigate:
    url: string * [<Optional>] ?cancellationToken: CancellationToken -> Tasks.Task<Result<unit, NavigationError<'View>>>

  member NavigateByName:
    routeName: string *
    [<Optional>] ?routeParams: IReadOnlyDictionary<string, obj> *
    [<Optional>] ?cancellationToken: CancellationToken ->
      Tasks.Task<Result<unit, NavigationError<'View>>>

  member CanGoBack: bool
  member CanGoForward: bool

  member Back: [<Optional>] ?cancellationToken: CancellationToken -> Tasks.Task<Result<unit, NavigationError<'View>>>

  member Forward: [<Optional>] ?cancellationToken: CancellationToken -> Tasks.Task<Result<unit, NavigationError<'View>>>
