namespace TGUI

open System
open System.Runtime.CompilerServices
open Terminal.Gui.ViewBase
open Terminal.Gui.Views

[<Extension>]
type ViewExtensions() =

  [<Extension>]
  static member inline Content(this: #View, [<ParamArray>] views: View array) =
    this.RemoveAll() |> ignore

    for view in views do
      this.Add view |> ignore

    this

  [<Extension>]
  static member inline Title(this: #View, title: string) =
    this.Title <- title
    this

  [<Extension>]
  static member inline Enabled(this: #View, enabled: bool) =
    this.Enabled <- enabled
    this

  [<Extension>]
  static member inline Visible(this: #View, visible: bool) =
    this.Visible <- visible
    this

  [<Extension>]
  static member inline Text(this: #View, text: string) =
    this.Text <- text
    this

  [<Extension>]
  static member inline X(this: #View, pos: Pos) =
    this.X <- pos
    this

  [<Extension>]
  static member inline X(this: #View, value) =
    this.X <- Pos.op_Implicit value
    this

  [<Extension>]
  static member inline Y(this: #View, pos: Pos) =
    this.Y <- pos
    this

  [<Extension>]
  static member inline Y(this: #View, value) =
    this.Y <- Pos.op_Implicit value
    this

  [<Extension>]
  static member inline Width(this: #View, dim: Dim | null) =
    this.Width <- dim
    this

  [<Extension>]
  static member inline Height(this: #View, dim: Dim | null) =
    this.Height <- dim
    this

[<Extension>]
type TextFieldExtensions() =

  [<Extension>]
  static member inline Secret(this: #TextField, isSecret: bool) =
    this.Secret <- isSecret
    this

[<Extension>]
type ButtonExtensions() =

  [<Extension>]
  static member inline IsDefault(this: #Button, isDefault: bool) =
    this.IsDefault <- isDefault
    this

  [<Extension>]
  static member inline OnAccept(this: #Button, [<InlineIfLambda>] action) =
    this.Accepted.Add action
    this

[<AutoOpen>]
type Constructors =
  static member inline Label(?text) = new Label(Text = defaultArg text "")

  static member inline TextField(?text) =
    new TextField(Text = defaultArg text "")

  static member inline Button(?text) = new Button(Text = defaultArg text "")
  static member inline Window(?title) = new Window(Title = defaultArg title "")
  static member inline FrameView() = new FrameView()

  static member inline View([<ParamArray>] views: View array) =
    let value = new View()
    value.Add(views)
    value

  static member inline Pos(value: int) = Pos.op_Implicit value
