namespace GooSample

open Goo

/// Data in. The struct keeps structural equality, so a parent can
/// compare one snapshot with the next and skip a rebuild.
[<Struct>]
type CounterProps = { Initial: int; Step: int }

module Counter =

  /// Plain view. A test can call this without a Cell or a Window.
  let view (props: CounterProps, count: int, onIncrement: unit -> unit) : Blob =
    let root = Container(FlexDirection = FlexDirection.Column, Gap = 8)
    root.Children.Add(Styles.body $"Count {count}")
    root.Children.Add(Styles.button("Add", onIncrement))
    root

  /// Creates the stateful Cell. Local state lives here, not in Props.
  let create (props: CounterProps, countChanged: int -> unit) =
    let mutable count = props.Initial

    let onIncrement () =
      count <- count + props.Step
      countChanged count

    { new Cell() with
        override _.Build() = view(props, count, onIncrement)
    }
