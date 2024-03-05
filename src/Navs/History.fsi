namespace Navs

type IHistoryManager<'HistoryEntry> =
  abstract member CanGoBack: bool
  abstract member CanGoForward: bool
  abstract member SetCurrent: 'HistoryEntry -> unit

  abstract member Current: 'HistoryEntry voption

  abstract member Next: unit -> 'HistoryEntry voption
  abstract member Previous: unit -> 'HistoryEntry voption

type internal HistoryManager<'HistoryEntry> =
  new: ?historySize: int -> HistoryManager<'HistoryEntry>
  interface IHistoryManager<'HistoryEntry>
