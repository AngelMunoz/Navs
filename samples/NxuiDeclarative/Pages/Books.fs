module Pages.Books

open System
open System.Threading
open System.Threading.Tasks
open IcedTasks
open NXUI.Extensions
open Avalonia.Controls
open Avalonia.Controls.Templates
open FSharp.Data.Adaptive
open IcedTasks
open Navs
open Navs.Avalonia

let private bookTitles = [
  "StarCraft: Liberty's Crusade"
  "StarCraft: The Queen of Blades"
  "StarCraft: Ghost - Nova"
  "Halo: The Fall of Reach"
  "Halo: The Flood"
]

type private ViewState =
  | Loading
  | Idle

let private loadBooks (setState, setBooks) (query: string voption) =
  asyncEx {
    setState(fun _ -> Loading)

    let foundBooks =
      match query with
      | ValueSome title ->
        let books =
          // try a fuzzy match search
          bookTitles
          |> List.filter(fun t ->
            t.IndexOf(title, StringComparison.InvariantCultureIgnoreCase) >= 0
          )

        if List.isEmpty books then
          [ $"No books found for '{title}'" ]
        else
          books
      | ValueNone -> bookTitles
    // Simulate a delay to mimic fetching data
    do! Async.Sleep(1000)
    setBooks(fun _ -> foundBooks)
    setState(fun _ -> Idle)
  }
  |> Async.StartImmediate

let private bookTemplate =
  FuncDataTemplate<string>(fun title _ ->
    TextBlock()
      .Text(title)
      .FontSize(16.)
      .Margin(8.)
      .HorizontalAlignmentCenter()
      .VerticalAlignmentCenter()
  )

let private Header () =
  TextBlock()
    .Text("Books")
    .FontSize(24.)
    .Margin(8.)
    .HorizontalAlignmentCenter()
    .VerticalAlignmentCenter()

let private PageContent (state, books) =
  let content =
    state
    |> AVal.map(fun state ->
      match state with
      | Loading -> TextBlock().Text("Loading books...") :> Control
      | Idle ->
        ItemsControl()
          .ItemsSource(books |> AVal.toBinding)
          .ItemTemplate(bookTemplate)
    )
    |> AVal.toBinding

  UserControl()
    .Content(content)
    .HorizontalAlignmentStretch()
    .VerticalAlignmentStretch()

let view: AsyncView<Control> =
  fun context _ -> asyncEx {
    let query = context.getParam<string> "title"
    let state, setState = AVal.useState Loading
    let books, setBooks = AVal.useState<string list> []
    let loadBooks = loadBooks(setState, setBooks)

    loadBooks query

    return StackPanel().Children(Header(), PageContent(state, books)) :> Control
  }
