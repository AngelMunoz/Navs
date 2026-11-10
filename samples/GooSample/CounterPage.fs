namespace GooSample

open System
open Goo

module CounterPage =

  /// Plain view. A test can call this without a Cell or a Window.
  let view
    (props: CounterProps, goBack: unit -> unit, countChanged: int -> unit)
    : Blob =
    let counter =
      Cell.Mount<Cell>(
        Func<Cell>(fun () -> Counter.create(props, countChanged)),
        "counter"
      )

    Styles.page [
      Styles.title "Counter"
      counter
      Styles.button("Back", goBack)
    ]

  let create
    (props: CounterProps, goBack: unit -> unit, countChanged: int -> unit)
    =

    { new Cell() with
        override _.Build() = view(props, goBack, countChanged)
    }
