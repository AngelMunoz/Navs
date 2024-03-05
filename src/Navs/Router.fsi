namespace Navs.Router

open System
open System.Collections.Generic
open System.Threading
open System.Runtime.InteropServices
open FSharp.Data.Adaptive
open Navs

[<Struct; NoComparison>]
type ActiveRouteParams =
  { SegmentIndex: int
    ParamName: string
    ParamValue: string }

[<Struct; NoComparison; NoEquality>]
type NavigationError<'View> =
  | NavigationCancelled
  | RouteNotFound of url: string
  | CantDeactivate of deactivateGuard: RouteDefinition<'View>
  | CantActivate of activateGuard: RouteDefinition<'View>

type Router<'View> =
  new:
    routes: RouteTrack<'View> seq *
    [<Optional>] ?splash: Func<'View> *
    [<Optional>] ?notFound: Func<'View> *
    [<Optional>] ?historyManager: IHistoryManager<RouteTrack<'View>> ->
      Router<'View>

  member Content: IObservable<'View voption>
  member AdaptiveContent: aval<'View voption>

  member Navigate:
    url: string * [<Optional>] ?cancellationToken: CancellationToken -> Tasks.Task<Result<unit, NavigationError<'View>>>

  member NavigateByName:
    routeName: string *
    [<Optional>] ?routeParams: IReadOnlyDictionary<string, obj> *
    [<Optional>] ?cancellationToken: CancellationToken ->
      Tasks.Task<Result<unit, NavigationError<'View>>>
