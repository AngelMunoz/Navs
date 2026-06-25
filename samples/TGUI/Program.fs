open Terminal.Gui
open Terminal.Gui.App
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open System
open TGUI

open Navs
open UrlTemplates.RouteMatcher
open Navs.Terminal.Gui


let Login app (ctx: RouteContext) (navigable: INavigable<_>) =
  let window =
    Window
      $"Example App ({App.Application.GetDefaultKey Input.Command.Quit} to quit)"

  let username =
    UrlMatch.getParamFromQuery "username" ctx.urlMatch |> Option.ofValueOption


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
      .OnAccept(fun _ -> navigable.NavigateByName("home") |> Task.FireAndForget)

  window.Content(
    usernameLabel,
    userNameText,
    passwordLabel,
    passwordText,
    btnLogin,
    backButton
  )

let Home _ (navigable: INavigable<Window>) =
  let label = Label("Welcome to the Home Page")

  let homeBtn =
    Button("About")
      .Y(Pos.Bottom(label))
      .OnAccept(fun _ ->
        navigable.NavigateByName("about")
        |> Async.AwaitTask
        |> Async.Ignore
        |> Async.StartImmediate
      )

  let login =
    Button("Login")
      .Y(Pos.Bottom(homeBtn))
      .OnAccept(fun _ ->
        navigable.NavigateByName("login") |> Task.FireAndForget
      )

  Window("Home").Content(label, homeBtn, login)

let About _ (navigable: INavigable<Window>) =
  let label = Label("Welcome to the About Page")

  let homeBtn =
    Button("Home")
      .Y(Pos.Bottom(label))
      .OnAccept(fun _ -> navigable.NavigateByName("home") |> Task.FireAndForget

      )

  let login =
    Button("Login")
      .Y(Pos.Bottom(homeBtn))
      .OnAccept(fun _ ->
        navigable.NavigateByName("login") |> Task.FireAndForget
      )

  Window("About").Content(label, homeBtn, login)



[<EntryPoint; STAThread>]
let main argv =
  // dotnet run --project samples/TGUI -- tgui:///login?username=admin
  let app = Application.Create().Init()

  let routes = [
    Route.define("home", "/", Home)
    Route.define("about", "/about", About)
    Route.define("login", "/login?username", Login app)
  ]

  let router: IRouter<Window> = TerminalGuiRouter routes

  let deepLink =
    argv
    |> Array.tryPick(fun arg ->
      try
        let uri = Uri(arg)
        if uri.Scheme = "tgui" then Some(uri) else None
      with _ ->
        None
    )
    |> Option.map(fun url -> url.PathAndQuery + url.Fragment)
    |> Option.defaultValue("/")

  router.Navigate deepLink |> Task.FireAndForget

  app.Run(new RouterOutlet(router)) |> ignore
  // Before the application exits, reset Terminal.Gui for clean shutdown
  app.RequestStop()

  0 // return an integer exit code
