---
index: 1
title: Usage from Non F# languages.
category: Navs
---

## Usage from Non F# languages.

Navs is meant to be used in the dotnet platform and that includes languages like C# and VB.NET. This is a guide on how to use Navs from non F# languages.

For the most part `Navs` works in the same way it does, but there is a type worth aliasing in order to make the API more idiomatic to the language you are using.

```csharp
using Navs;
using Navs.Router;

// Namespaces for smooth interop
// With the library for non-F# languages.
using Route = Navs.Interop.Route;
```

The aliased `Route` is a static class that contains `Route.Define` method and overloads that expect `Func<_>` rather than `FSharpFunc<_>`. which will make the API more idiomatic to the language you are using.

```csharp

RouteDefinition<string>[] routes = [
    Route.Define<string>("home", "/", () => "Home"),
    Route.Define<string>("about", "/about", () => "About"),
    Route.Define<string>("contact", "/contact", () => "Contact"),
    Route.Define<string>("random", "/random", async (_, token) => {
        await Task.Delay(90, token);
        return $"Random {Random.Shared.Next()}";
    })
];

var router = new Router<string>(RouteTracks.FromDefinitions(routes));
```

From there on, you can use the router as you would in F#. for more information visit the [Navs](../Navs.fsx) general documentation.
