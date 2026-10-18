namespace Navs.Adapters.Tests

open Avalonia
open Avalonia.Headless
open Expecto

type TestApplication() =
  inherit Application()

module Main =
  [<EntryPoint>]
  let main argv =
    // Avalonia controls need a platform before any test creates one. This used to live
    // in a test, which runs after the adapter tests that build TextBlocks and outlets.
    printfn "Initializing the Avalonia headless platform..."
    AppBuilder
      .Configure<TestApplication>()
      .UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = true))
      .SetupWithoutStarting()
    |> ignore

    printfn "Avalonia headless platform ready."
    runTestsInAssemblyWithCLIArgs [] argv
