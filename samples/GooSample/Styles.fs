namespace GooSample

open Goo
open Goo.Widgets

/// Goo theme helpers for this sample. Navs ships no styling of its own.
/// Each helper reads the current Goo theme inside a Cell build, so the
/// page follows the system light or dark palette after Theme.Attach.
/// Call these helpers only from a Cell build or a view it calls.
module Styles =

  let page (children: Blob seq) : Blob =
    let theme = Theme.Read()

    let root =
      Container(
        BasedOn = theme.CanvasStyle,
        FlexDirection = FlexDirection.Column,
        Padding = Length.Pixels(theme.Spacing * 6.0),
        Gap = Length.Pixels(theme.Spacing * 3.0),
        Width = Length.Percent(100.0),
        Height = Length.Percent(100.0)
      )

    children |> Seq.iter(fun child -> root.Children.Add(child))
    root

  let title (text: string) : Blob =
    let theme = Theme.Read()

    Text(
      Content = text,
      FontSize = theme.H2FontSize,
      FontWeight = float theme.HeadingFontWeight,
      Color = theme.TextColor
    )

  let body (text: string) : Blob =
    let theme = Theme.Read()

    Text(
      Content = text,
      FontSize = theme.FontSize,
      Color = theme.MutedTextColor
    )

  let button (label: string, onClick: unit -> unit) : Blob =
    let theme = Theme.Read()

    let button =
      Button(
        OnClick = onClick,
        BackgroundColor = theme.AccentColor,
        Padding = theme.ControlPadding,
        BorderRadius = theme.ControlRadius.GetValueOrDefault(6.0),
        Hover = Style(BackgroundColor = theme.AccentHoverColor)
      )

    button.Children.Add(
      Text(
        Content = label,
        FontSize = theme.FontSize,
        Color = theme.OnAccentColor
      )
    )

    button
