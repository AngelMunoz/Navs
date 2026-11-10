namespace GooSample

open Goo

/// Data in. The struct keeps structural equality.
[<Struct>]
type HomeProps = { Title: string }

module Home =

  /// Plain view. A test can call this without a Cell or a Window.
  let view
    (props: HomeProps, openCounter: unit -> unit, openAbout: unit -> unit)
    : Blob =
    Styles.page [
      Styles.title props.Title
      Styles.button("Counter", openCounter)
      Styles.button("About", openAbout)
    ]

  let create
    (props: HomeProps, openCounter: unit -> unit, openAbout: unit -> unit)
    =

    { new Cell() with
        override _.Build() = view(props, openCounter, openAbout)
    }
