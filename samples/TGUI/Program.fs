open System
open System.Threading

open Terminal.Gui
open Terminal.Gui.App
open Terminal.Gui.ViewBase
open Terminal.Gui.Views

open Navs
open Navs.Router
open Navs.Terminal.Gui
open TGUI

let navigateByName (name: string) (navigable: INavigable) =
  navigable.NavigateByName (name, null) CancellationToken.None |> ignore

let Login app (context: RouteContext) (navigable: INavigable) =
  let window =
    Window
      $"Example App ({App.Application.GetDefaultKey Input.Command.Quit} to quit)"

  let username = context.GetParam<string>("username") |> ValueOption.toOption

  let usernameLabel = Label("Username:")

  let userNameText =
    TextField(?text = username)
      .X(Pos.Right(usernameLabel) + Pos(1))
      .Width(Dim.Fill())

  let passwordLabel =
    Label("Password:")
      .X(Pos.Left(usernameLabel))
      .Y(Pos.Bottom(usernameLabel) + Pos(1))

  let passwordText =
    TextField()
      .Secret(true)
      .X(Pos.Left(userNameText))
      .Y(Pos.Top(passwordLabel))
      .Width(Dim.Fill())

  let btnLogin =
    Button("Login")
      .X(Pos.Bottom(passwordLabel) + Pos(1))
      .Y(Pos.Center())
      .IsDefault(true)
      .OnAccept(fun _ ->
        if userNameText.Text = "admin" && passwordText.Text = "password" then
          MessageBox.Query(app, "Logging In", "Login Successful", "Ok")
          |> ignore

          app.RequestStop()
        else
          MessageBox.ErrorQuery(
            app,
            "Logging In",
            "Incorrect username or password",
            "Ok"
          )
          |> ignore
      )

  let backButton =
    Button("Home")
      .X(Pos.Bottom(btnLogin) + Pos(1))
      .Y(Pos.Center())
      .OnAccept(fun _ -> navigateByName "home" navigable)

  window.Content(
    usernameLabel,
    userNameText,
    passwordLabel,
    passwordText,
    btnLogin,
    backButton
  )

let Home _ (navigable: INavigable) =
  let label = Label("Welcome to the Home Page")

  let homeBtn =
    Button("About")
      .Y(Pos.Bottom(label))
      .OnAccept(fun _ -> navigateByName "about" navigable)

  let login =
    Button("Login")
      .Y(Pos.Bottom(homeBtn))
      .OnAccept(fun _ -> navigateByName "login" navigable)

  Window("Home").Content(label, homeBtn, login)

let About _ (navigable: INavigable) =
  let label = Label("Welcome to the About Page")

  let homeBtn =
    Button("Home")
      .Y(Pos.Bottom(label))
      .OnAccept(fun _ -> navigateByName "home" navigable)

  let login =
    Button("Login")
      .Y(Pos.Bottom(homeBtn))
      .OnAccept(fun _ -> navigateByName "login" navigable)

  Window("About").Content(label, homeBtn, login)

[<EntryPoint; STAThread>]
let main argv =
  let app = Application.Create().Init()

  let routes = [
    Route.define<Window>("home", "/", Home)
    Route.define<Window>("about", "/about", About)
    Route.define<Window>("login", "/login?username", Login app)
  ]

  let router = TerminalGuiRouter(routes) :> IRouter<Window>

  let deepLink =
    argv
    |> Array.tryPick(fun arg ->
      try
        let uri = Uri(arg)
        if uri.Scheme = "tgui" then Some uri else None
      with _ ->
        None
    )
    |> Option.map(fun url -> url.PathAndQuery + url.Fragment)
    |> Option.defaultValue "/"

  router.Navigate (deepLink) CancellationToken.None |> ignore

  app.Run(new RouterOutlet(router)) |> ignore
  app.RequestStop()

  0
