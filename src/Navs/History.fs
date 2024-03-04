namespace Navs

open System.Collections.Generic
open IcedTasks
open FsToolkit.ErrorHandling


type IHistoryManager<'HistoryEntry> =
  abstract member CanGoBack: bool
  abstract member CanGoForward: bool
  abstract member SetCurrent: 'HistoryEntry -> unit

  abstract member Current: 'HistoryEntry voption

  abstract member Next: unit -> 'HistoryEntry voption
  abstract member Previous: unit -> 'HistoryEntry voption


type HistoryManager<'HistoryEntry>(?historySize: int) =
  let historySize = defaultArg historySize 10

  let history = LinkedList<'HistoryEntry>()
  let forwardHistory = LinkedList<'HistoryEntry>()

  interface IHistoryManager<'HistoryEntry> with

    member val CanGoBack = history.Count > 1 with get
    member val CanGoForward = forwardHistory.Count > 0 with get

    member _.Current =
      history.Last
      |> ValueOption.ofNull
      |> ValueOption.map(fun value -> value.Value)

    member _.SetCurrent(route: 'HistoryEntry) =
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
