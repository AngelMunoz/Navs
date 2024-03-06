using Navs;
using Navs.Interop;
using Navs.Router;
using Navs.Avalonia;
using Route = Navs.Avalonia.Interop.Route;

AppBuilder
  .Configure<Application>()
  .UsePlatformDetect()
  .UseFluentTheme()
  .WithApplicationName("NXUI With Router!")
  .StartWithClassicDesktopLifetime(GetWindow, args);

static IEnumerable<RouteDefinition<Control>> GetRoutes() => [
     Route.Define("home", "/", ctx => new TextBlock().Text("Hello World!"))
      .NoCacheOnVisit()
      .Children(
        Route.Define("sub", "sub/route", ctx => new TextBlock().Text("Sub")),
        Route.Define("sub2", "sub/route2", ctx => new TextBlock().Text("Sub2"))
       ),
     Route.Define("about", "/about", ctx => new TextBlock().Text("About")),
     Route.Define("by-name", "/by-name/:id<guid>", async (ctx, token) => {
        // Simulate a fetch or something
        await Task.Delay(80, token);
        ctx.UrlMatch.Params.TryGetValue("id", out var id);
        return new TextBlock().Text($"By Name: {id as Guid?}");
     })
  ];

static Window GetWindow()
{
  var router = new AvaloniaRouter(GetRoutes());

  var content = router.Content.Select(view => view.IsSome ? view.Value : new TextBlock().Text("Not Found"));

  return new Window().Title("Hello World!").Content(
    StackPanel().Children(
      Button().Content("Home").OnClickHandler((_, _) => NavigateTo("/", router)),
      Button().Content("About").OnClickHandler((_, _) => NavigateTo("/about", router)),
      Button()
        .Content("Navigate By Name")
        .OnClickHandler((_, _) => NavigateByName("by-name", router, new Dictionary<string, object> { { "id", Guid.NewGuid() } })),
      Button()
        .Content("Navigate By Name Missing Param")
        .OnClickHandler((_, _) => NavigateByName("by-name", router)),
      ContentControl().Content(content.ToBinding(), BindingMode.OneWay)
    )
  );
}

static async void NavigateByName(string name, Router<Control> router, Dictionary<string, object>? routeParams = null)
{
  var result = await router.NavigateByName("by-name", routeParams);

  if (result.IsError) { Console.WriteLine($"Error: {result.ErrorValue}"); }
  else
  {
    Console.WriteLine($"Navigated to: {name}");
  }
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
