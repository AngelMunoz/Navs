open System
open System.Collections.Generic
open System.Threading
open Goo
open Navs
open Navs.Router
open Navs.Goo
open Goo.Widgets
open GooSample

let navigateByName (navigable: INavigable, name: string) =
  navigable.NavigateByName
    (name, Dictionary<string, obj>())
    CancellationToken.None
  |> ignore

let mount (key: string, create: unit -> Cell) : Blob =
  Cell.Mount<Cell>(Func<Cell>(create), key)

let homeRoute (_: RouteContext) (navigable: INavigable) =
  let props: HomeProps = { Title = "Home" }
  let openCounter () = navigateByName(navigable, "counter")
  let openAbout () = navigateByName(navigable, "about")

  mount("home", fun () -> Home.create(props, openCounter, openAbout))

let counterRoute (_: RouteContext) (navigable: INavigable) =
  let props: CounterProps = { Initial = 0; Step = 1 }

  let goBack () =
    navigable.Back () CancellationToken.None |> ignore

  let countChanged value = Console.WriteLine($"Count {value}")

  mount(
    "counter-page",
    fun () -> CounterPage.create(props, goBack, countChanged)
  )

let aboutRoute (_: RouteContext) (navigable: INavigable) =
  let props: AboutProps = {
    Title = "About"
    Body = "Navs routes. Goo renders."
  }

  let openHome () = navigateByName(navigable, "home")

  mount("about", fun () -> About.create(props, openHome))

[<EntryPoint>]
let main _ =
  Window.ConfigureApplication(
    "Navs.Goo sample",
    "1.0.0",
    "io.github.navs.goosample"
  )

  let routes = [
    Route.define<Blob>("home", "/", homeRoute)
    Route.define<Blob>("counter", "/counter", counterRoute)
    Route.define<Blob>("about", "/about", aboutRoute)
  ]

  let router = GooRouter(routes) :> IRouter<Blob>

  let outlet = new RouterOutlet(initialUri = "/")
  outlet.NoContent <- Some(Text(Content = "Loading") :> Blob)
  outlet.Router <- Some router

  let window =
    Window(Title = "Navs.Goo sample", Width = 480, Height = 320, Root = outlet)

  outlet.Window <- Some window
  Theme.Attach(window)
  outlet.Start()
  window.Run()
  0
