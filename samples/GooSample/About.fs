namespace GooSample

open Goo

/// Data in. The struct keeps structural equality.
[<Struct>]
type AboutProps = { Title: string; Body: string }

module About =

  /// Plain view. A test can call this without a Cell or a Window.
  let view (props: AboutProps, openHome: unit -> unit) : Blob =
    Styles.page [
      Styles.title props.Title
      Styles.body props.Body
      Styles.button("Home", openHome)
    ]

  let create (props: AboutProps, openHome: unit -> unit) =

    { new Cell() with
        override _.Build() = view(props, openHome)
    }
