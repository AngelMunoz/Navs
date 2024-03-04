using Navs;
using Navs.Interop;
using Navs.Router;
using Route = Navs.Interop.Route;

NXUI.Desktop.NXUI.Run(GetWindow, "NXUI Routered!", args);

static IEnumerable<RouteDefinition<Control>> GetRoutes()
{
  return [
     Route.Define<Control>("home", "/", ctx => new TextBlock().Text("Hello World!")),
     Route.Define<Control>("about", "/about", ctx => new TextBlock().Text("About")),
  ];
}

static async void NavigateTo(string path, Router<Control> router)
{
  var result = await router.Navigate(path);

  if (result.IsError) { Console.WriteLine($"Error: {result.ErrorValue}"); }
  else
  {
    Console.WriteLine($"Navigated to: {path}");
  }
}

static Window GetWindow()
{
  var routes = RouteTracks.FromDefinitions(GetRoutes());
  var router = new Router<Control>(routes);

  var content = router.Content.Select(view =>
  {
    return view.IsSome ? view.Value : new TextBlock().Text("Not Found");
  });


  return new Window().Title("Hello World!").Content(
    StackPanel().Children(
      Button().Content("Home").OnClickHandler((_, _) => NavigateTo("/", router)),
      Button().Content("About").OnClickHandler((_, _) => NavigateTo("/about", router)),
      ContentControl().Content(content.ToBinding(), BindingMode.OneWay)
    )
  );
}

