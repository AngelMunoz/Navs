# The Navs Family

Welcome to the Navs Family documentation.

This documentation is a work in progress.

This project contains the following libraries:

- [Navs](#Navs)
- [Navs.Avalonia](#Navs.Avalonia)
- [UrlTemplates](#UrlTemplates)

### Navs

Navs is a router-like abstraction inspired by web routers such as vue-router, angular-router and similar projects.

It is primarily a "core" library which you would usually depend on in your own projects, as it is very generic and while F# can be very intelligent about type inference, it tends to produce quite verbose signatures. For more information visit the Navs section in these docs.

- [Navs](./Navs/Index.md)

### Navs.Avalonia

This project attempts to hide the generics from call sites and offer a few DSLs to make it easier to use Navs in Avalonia applications. This router was designed to be used with Raw Avalonia Control classes, however it will pair very nicely with the [NXUI]() project, Feel free to check the C# and F# samples in the [Samples]() folder in the source code repository.

- [Navs.Avalonia](./Navs.Avalonia/index.md)

### UrlTemplates

This is a library for parsing URL-like strings into structured objects. It is used by Navs to parse navigable URLs and URL templates to find if they match.

Currently this library is mainly aimed to be used from F# but if there's interest in using it from C# I can add some more friendly APIs.

- [UrlTemplates](./UrlTemplates/index.md)
