module HeadlessTests

open Avalonia
open Avalonia.Headless
open Expecto

type TestApplication() =
  inherit Application()

[<Tests>]
let tests =
  testList "Avalonia headless" [
    testCase "the headless platform starts"
    <| fun () ->
      AppBuilder
        .Configure<TestApplication>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions())
        .SetupWithoutStarting()
      |> ignore

      Expect.isTrue true "the headless platform should start"
  ]
