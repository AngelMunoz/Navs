namespace TGUI

open Terminal.Gui
open System
open System.Runtime.CompilerServices
open FSharp.Data.Adaptive

[<Extension>]
type ViewExtensions() =

  [<Extension>]
  static member inline Content(this: #View, [<ParamArray>] views: View array) =
    this.RemoveAll()

    for view in views do
      this.Add view |> ignore

    this

  [<Extension>]
  static member inline Content(this: #View, view: View aval) =
    let disp =
      view.AddWeakCallback(fun (v: View) ->
        this.RemoveAll()
        this.Add(v) |> ignore
      )

    this.Disposing.Add(fun _ -> disp.Dispose())
    this

  [<Extension>]
  static member inline Title(this: #View, title: string) =
    this.Title <- title
    this

  [<Extension>]
  static member inline Title(this: #View, title: string aval) =
    let dis = title.AddCallback(fun value -> this.Title <- value)
    this.Disposing.Add(fun _ -> dis.Dispose())
    this

  [<Extension>]
  static member inline Title(this: #View, title: string cval) =
    let dis =
      this.TitleChanged
      |> Observable.subscribe(fun s ->
        transact(fun () -> title.Value <- this.Title)
      )

    let dis2 = title.AddCallback(fun value -> this.Title <- value)

    this.Disposing.Add(fun _ ->
      dis.Dispose()
      dis2.Dispose()
    )

    this

  [<Extension>]
  static member inline Enabled(this: #View, enabled: bool) =
    this.Enabled <- enabled
    this

  [<Extension>]
  static member inline Enabled(this: #View, enabled: bool aval) =
    let dis = enabled.AddCallback(fun value -> this.Enabled <- value)
    this.Disposing.Add(fun _ -> dis.Dispose())
    this

  [<Extension>]
  static member inline Enabled(this: #View, enabled: bool cval) =
    let dis =
      this.EnabledChanged
      |> Observable.subscribe(fun s ->
        transact(fun () -> enabled.Value <- this.Enabled)
      )

    let dis2 = enabled.AddCallback(fun value -> this.Enabled <- value)

    this.Disposing.Add(fun _ ->
      dis.Dispose()
      dis2.Dispose()
    )

    this

  [<Extension>]
  static member inline Visible(this: #View, visible: bool) =
    this.Visible <- visible
    this

  [<Extension>]
  static member inline Visible(this: #View, visible: bool aval) =
    let dis = visible.AddCallback(fun value -> this.Visible <- value)
    this.Disposing.Add(fun _ -> dis.Dispose())
    this

  [<Extension>]
  static member inline Visible(this: #View, visible: bool cval) =
    let dis =
      this.VisibleChanged
      |> Observable.subscribe(fun s ->
        transact(fun () -> visible.Value <- this.Visible)
      )

    let dis2 = visible.AddCallback(fun value -> this.Visible <- value)

    this.Disposing.Add(fun _ ->
      dis.Dispose()
      dis2.Dispose()
    )

    this

  [<Extension>]
  static member inline Text(this: #View, text: string) =
    this.Text <- text
    this

  [<Extension>]
  static member inline Text(this: #View, title: string aval) =
    let dis = title.AddCallback(fun value -> this.Text <- value)
    this.Disposing.Add(fun _ -> dis.Dispose())
    this

  [<Extension>]
  static member inline Text(this: #View, title: string cval) =
    let dis =
      this.TextChanged
      |> Observable.subscribe(fun s ->
        transact(fun () -> title.Value <- this.Title)
      )

    let dis2 = title.AddCallback(fun value -> this.Text <- value)

    this.Disposing.Add(fun _ ->
      dis.Dispose()
      dis2.Dispose()
    )

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
    this.Accept.Add(action)
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
    let v = new View()
    v.Add(views)
    v

  static member inline Pos(value: int) = Pos.op_Implicit value


module Task =
  let inline FireAndForget (task: System.Threading.Tasks.Task) =
    System.Threading.Tasks.Task.Run(fun () -> task) |> ignore
