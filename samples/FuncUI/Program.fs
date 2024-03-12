open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes

open Avalonia.FuncUI
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.DSL

open Navs
open Navs.FuncUI
open Avalonia.Themes.Fluent
open Avalonia.FuncUI.Types

let navbar (router: FuncUIRouter) : IView =
  StackPanel.create [
    StackPanel.dock Dock.Top
    StackPanel.orientation Layout.Orientation.Horizontal
    StackPanel.children [
      Button.create [
        Button.content "Books"
        Button.onClick(fun _ -> router.Navigate "/books" |> ignore)
      ]
      Button.create [
        Button.content "Guid"
        Button.onClick(fun _ -> router.Navigate $"/{Guid.NewGuid()}" |> ignore)
      ]
    ]
  ]

let routes = [
  Route.define(
    "books",
    "/books",
    (fun _ -> TextBlock.create [ TextBlock.text "Books" ])
  )
  Route.define(
    "guid",
    "/:id<guid>",
    fun (context, _) -> async {
      return
        TextBlock.create [
          match context.urlMatch.Params.TryGetValue "id" with
          | true, id -> TextBlock.text $"Visited: {id}"
          | false, _ -> TextBlock.text "Guid No GUID"
        ]
    }
  )
]

let appContent (router: FuncUIRouter, navbar: FuncUIRouter -> IView) =
  Component(fun ctx ->

    let currentView = ctx.useRouter router

    DockPanel.create [
      DockPanel.lastChildFill true
      DockPanel.children [ navbar router; currentView.Current ]
    ]
  )


type AppWindow(router: FuncUIRouter) as this =
  inherit HostWindow()

  do this.Content <- appContent(router, navbar)


type App() =
  inherit Application()

  override this.Initialize() = this.Styles.Add(FluentTheme())

  override this.OnFrameworkInitializationCompleted() =
    let router = FuncUIRouter routes
    let window = AppWindow router

    match this.ApplicationLifetime with
    | :? IClassicDesktopStyleApplicationLifetime as desktop ->
      desktop.MainWindow <- window :> Window
    | _ -> ()


AppBuilder
  .Configure<App>()
  .UsePlatformDetect()
  .StartWithClassicDesktopLifetime(System.Environment.GetCommandLineArgs())
|> ignore
