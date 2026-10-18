open System
open System.Threading

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

open Avalonia.FuncUI
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types

open Navs
open Navs.Router
open Navs.FuncUI

let navbar (router: IRouter<IView>) : IView =
  StackPanel.create [
    StackPanel.dock Dock.Top
    StackPanel.orientation Layout.Orientation.Horizontal
    StackPanel.children [
      Button.create [
        Button.content "Books"
        Button.onClick(fun _ ->
          router.Navigate ("/books") CancellationToken.None |> ignore
        )
      ]
      Button.create [
        Button.content "Guid"
        Button.onClick(fun _ ->
          router.Navigate ($"/{Guid.NewGuid()}") CancellationToken.None
          |> ignore
        )
      ]
    ]
  ]

let routes = [
  Route.define<IView>(
    "books",
    "/books",
    fun _ _ -> TextBlock.create [ TextBlock.text "Books" ] :> IView
  )
  Route.define<IView>(
    "guid",
    "/:id<guid>",
    fun context _ -> async {
      return
        TextBlock.create [
          match context.GetParam<Guid>("id") with
          | ValueSome id -> TextBlock.text $"Visited: {id}"
          | ValueNone -> TextBlock.text "Guid No GUID"
        ]
        :> IView
    }
  )
]

let appContent (router: IRouter<IView>) =
  Component(fun context ->
    let noContent = TextBlock.create [ TextBlock.text "No content" ]

    DockPanel.create [
      DockPanel.lastChildFill true
      DockPanel.children [
        navbar router
        RouterOutlet.create(router, noContent = noContent)
      ]
    ]
  )

type AppWindow(router: IRouter<IView>) as this =
  inherit HostWindow()

  do this.Content <- appContent router

type App() =
  inherit Application()

  override this.Initialize() = this.Styles.Add(FluentTheme())

  override this.OnFrameworkInitializationCompleted() =
    let router = FuncUIRouter(routes) :> IRouter<IView>
    let window = AppWindow router

    match this.ApplicationLifetime with
    | :? IClassicDesktopStyleApplicationLifetime as desktop ->
      desktop.MainWindow <- window :> Window
    | _ -> ()

AppBuilder
  .Configure<App>()
  .UsePlatformDetect()
  .StartWithClassicDesktopLifetime(Environment.GetCommandLineArgs())
|> ignore
