namespace Navs.Goo

open System
open Microsoft.Extensions.Logging
open Goo
open Navs

/// A core Navs router bound to Goo Blob values.
type GooRouter =
  interface IRouter<Blob>

  new: routes: RouteDefinition<Blob> seq * ?splash: Func<Blob> * ?logger: ILogger -> GooRouter

/// Renders the active route as a Goo Cell. Set Router after creation.
/// Set Window after the Window that owns this outlet exists, so change
/// notices from other threads can reach the UI thread with Window.Post.
/// Call Start, or set both Router and an initial URI, for first navigation.
type RouterOutlet =
  inherit Cell
  interface IDisposable

  new: ?initialUri: string * ?logger: ILogger -> RouterOutlet

  member Router: IRouter<Blob> option with get, set

  member Window: Window option with get, set

  member NoContent: Blob option with get, set

  /// Starts the first navigation once, on the caller's thread. Call this
  /// on the UI thread, or after Window is set so the call can be posted.
  member Start: unit -> unit

  override Build: unit -> Blob
