using Navs.CSharp;

var routes = new[]
{
    Route.Define<string>("home", "/", (context, navigable) => "Home"),
    Route.Define<string>("about", "/about", (context, navigable) => "About")
};

var router = Router.Create(routes);

var home = await router.NavigateAsync("/");
Console.WriteLine(
    $"home: success={home.IsSuccess} route={home.Result?.Route.Path} content={router.Snapshot.Content} state={router.Snapshot.State}");

var about = await router.NavigateAsync("/about");
Console.WriteLine(
    $"about: success={about.IsSuccess} content={router.Snapshot.Content} history={router.Snapshot.History.Index}");

var back = await router.BackAsync();
Console.WriteLine(
    $"back: success={back.IsSuccess} content={router.Snapshot.Content} history={router.Snapshot.History.Index}");

var missing = await router.NavigateAsync("/missing");
Console.WriteLine(
    $"missing: success={missing.IsSuccess} error={missing.Error?.Code}");
