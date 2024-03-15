open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes

open Avalonia.FuncUI
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.DSL

open FSharp.Data.Adaptive

open Navs
open Navs.FuncUI
open Avalonia.Themes.Fluent
open Avalonia.FuncUI.Types

// use external to FuncUI shared state
let sharedState = cval 0

let navbar (router: IRouter<IView>) : IView =
  StackPanel.create [
    StackPanel.dock Dock.Top
    StackPanel.orientation Layout.Orientation.Horizontal
    StackPanel.children [
      Button.create [
        Button.content "Books"
        Button.onClick(fun _ ->
          router.Navigate "/books" |> ignore
          // example "external updates"
          transact(fun _ -> sharedState.Value <- sharedState.Value + 1)
        )
      ]
      Button.create [
        Button.content "Guid"
        Button.onClick(fun _ ->
          router.Navigate $"/{Guid.NewGuid()}" |> ignore
          transact(fun _ -> sharedState.Value <- sharedState.Value + 1)
        )
      ]
      Button.create [
        Button.content "Counter"
        Button.onClick(fun _ ->
          router.Navigate $"/counter" |> ignore
          transact(fun _ -> sharedState.Value <- sharedState.Value + 1)
        )
      ]
      Button.create [
        Button.content "Readable counter"
        Button.onClick(fun _ ->
          router.Navigate $"/read-counter" |> ignore
          transact(fun _ -> sharedState.Value <- sharedState.Value + 1)
        )
      ]
    ]
  ]


let routes = [
  Route.define(
    "books",
    "/books",
    (fun _ _ -> TextBlock.create [ TextBlock.text "Books" ])
  )
  Route.define(
    "guid",
    "/:id<guid>",
    fun context _ -> async {
      return
        TextBlock.create [
          match context.urlMatch.Params.TryGetValue "id" with
          | true, id -> TextBlock.text $"Visited: {id}"
          | false, _ -> TextBlock.text "Guid No GUID"
        ]
    }
  )
  |> Route.cache NoCache
  Route.define(
    "read-counter",
    "/read-counter",
    fun _ _ ->
      Component.create(
        "read-counter",
        fun ctx ->
          // consume adaptive data in the standard FuncUI way
          let counter = ctx.useAVal sharedState

          StackPanel.create [
            StackPanel.children [
              TextBlock.create [ TextBlock.text(counter.Current |> string) ]
            ]
          ]
      )
  )
  Route.define(
    "counter",
    "/counter",
    fun _ _ ->
      Component.create(
        "counter",
        fun ctx ->
          let counter = ctx.useCval sharedState

          let increment =
            Button.create [
              Button.content "Increment"
              Button.onClick(fun _ ->
                // values can also be updated using the standard FuncUI way
                counter.Set(counter.Current + 1) |> ignore
              )
            ]

          let decrement =
            Button.create [
              Button.content "Decrement"
              Button.onClick(fun _ ->
                counter.Set(counter.Current + 1) |> ignore
              )
            ]

          StackPanel.create [
            StackPanel.children [
              increment
              decrement
              TextBlock.create [ TextBlock.text(counter.Current |> string) ]
            ]
          ]
      )
  )
]

let appContent (router: FuncUIRouter, navbar: FuncUIRouter -> IView) =
  Component(fun ctx ->
    let noContent = TextBlock.create [ TextBlock.text "No content" ]

    DockPanel.create [
      DockPanel.lastChildFill true
      DockPanel.children [
        navbar router
        RouterOutlet.create(router, noContent)
      ]
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
