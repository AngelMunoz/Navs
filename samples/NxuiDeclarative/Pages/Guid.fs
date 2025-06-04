module Pages.Guid

open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Navs
open Navs.Avalonia
open NXUI.Extensions
open System

let view: SyncView<Control> =
  fun context _ ->
    let content =
      match context.getParam<Guid> "id" with
      | ValueSome guid -> TextBlock().Text($"Guid: {guid}")

      | ValueNone -> TextBlock().Text($"No Guid provided")

    content
      .FontSize(24.)
      .HorizontalAlignmentCenter()
      .VerticalAlignmentCenter()
      .Margin(16.)
    :> Control
