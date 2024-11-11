using FSharp.Data.Adaptive;
using CSharp.Data.Adaptive;

using Navs;
using UrlTemplates.RouteMatcher;
using Navs.Avalonia;

// Namespaces for smooth interop
// With the library for non-F# languages.
using Navs.Interop;
using Route = Navs.Avalonia.Interop.Route;
using static Navs.Avalonia.AVal.Interop;
using static Navs.Avalonia.RouterOutletExtensions;

AppBuilder
  .Configure<Application>()
  .UsePlatformDetect()
  .UseFluentTheme()
  .WithApplicationName("NXUI With Router!")
  .StartWithClassicDesktopLifetime(GetWindow, args);

static IEnumerable<RouteDefinition<Control>> GetRoutes() => [
     Route.Define("home", "/", (_ , _) => {
        var (count, setCount) = UseState(0);
        return StackPanel()
          .Spacing(8)
          .Children(
            TextBlock().Text("Home"),
            TextBlock().Text(count.Map(value => $"Count: {value}").ToBinding()),
            Button().Content("Increment").OnClickHandler((_, _) => setCount(count => count + 1)),
            Button().Content("Decrement").OnClickHandler((_, _) => setCount(count => count - 1)),
            Button().Content("Reset").OnClickHandler((_, _) => setCount(_ => 0))
          );
      }),
     Route.Define("about", "/about", (_ , _)=> {
        var text = new ChangeableValue<string>("");

        return StackPanel()
          .Spacing(8)
          .Children(
            TextBlock().Text("About"),
            TextBlock().Text("This is a simple Avalonia app with a router, It uses Navs for routing and Adaptive Data for state management."),
            TextBlock().Text(
              text.Map(value => {
                if (string.IsNullOrWhiteSpace(value)) { return "Type something!"; }
                return $"You typed: {value}";
              })
              .ToBinding()
            ),
            TextBox()
              .Watermark("Type here!")
              .OnTextChangedHandler((source,args) =>{
                if (source.Text is null) { return; }
                text.SetValue(text => source.Text);
              })
          );

     }),
     Route.Define<Control>("by-name", "/by-name?id<guid>", async (ctx, nav, token) => {
        // Simulate a fetch or something
        await Task.Delay(80, token);
        var guid = UrlMatchModule.getFromParams<Guid>("id", ctx.urlMatch);
        if (guid.IsValueSome && guid.Value is Guid id)
        {
          return TextBlock().Text($"By Name: {id}");
        }
        return StackPanel().Children(
          TextBlock().Text($"No ID Found!"),
          Button().Content("Visit one with Id")
            .OnClickHandler((_, _) => {
                nav.Navigate("/by-name?id=" + Guid.NewGuid());
            })
        );
     })
     .NoCacheOnVisit()
  ];

static Window GetWindow()
{
  IRouter<Control> router = new AvaloniaRouter(GetRoutes());

  return Window().Title("Hello World!").Content(
    StackPanel().Children(
      Button().Content("Home").OnClickHandler((_, _) => NavigateTo("/", router)),
      Button().Content("About").OnClickHandler((_, _) => NavigateTo("/about", router)),
      Button()
        .Content("Navigate By Name")
        .OnClickHandler((_, _) => NavigateByName("by-name", router, new Dictionary<string, object> { { "id", Guid.NewGuid() } })),
      Button()
        .Content("Navigate By Name Missing Param")
        .OnClickHandler((_, _) => NavigateByName("by-name", router)),
      RouterOutlet().Router(router)
    )
  );
}

static async void NavigateByName(string name, IRouter<Control> router, Dictionary<string, object>? routeParams = null)
{
  var result = await router.NavigateByName("by-name", routeParams);

  if (result.IsError) { Console.WriteLine($"Error: {result.ErrorValue}"); }
  else
  {
    Console.WriteLine($"Navigated to: {name}");
  }
}

static async void NavigateTo(string path, IRouter<Control> router)
{
  var result = await router.Navigate(path);

  Console.WriteLine(result.IsError ? $"Error: {result.ErrorValue}" : $"Navigated to: {path}");
}
