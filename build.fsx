#r "nuget: Fun.Result, 2.0.9"
#r "nuget: Fun.Build, 1.0.9"

open System.IO
open Fun.Build

let version = "1.0.0-beta-005"


let build name = stage $"Build {name}" { run $"dotnet build src/{name}" }

let pack name = stage $"Pack {name}" {
  run $"dotnet pack src/{name} -p:Version={version} -o dist"
}

let pushNugets = stage $"Push to NuGet" {

  run(fun ctx -> async {

    let nugetApiKey = ctx.GetEnvVar "NUGET_DEPLOY_KEY"
    let nugets = Directory.GetFiles(__SOURCE_DIRECTORY__ + "/dist", "*.nupkg")

    for nuget in nugets do
      printfn "Pushing %s" nuget

      let! res =
        ctx.RunSensitiveCommand
          $"dotnet nuget push {nuget} --skip-duplicate  -s https://api.nuget.org/v3/index.json -k {nugetApiKey}"

      match res with
      | Ok _ -> return ()
      | Error err -> failwith err
  })
}

pipeline "nuget" {

  build "UrlTemplates"
  build "Navs"
  build "Navs.Avalonia"
  build "Navs.FuncUI"
  pack "UrlTemplates"
  pack "Navs"
  pack "Navs.Avalonia"
  pack "Navs.FuncUI"
  pushNugets
  runIfOnlySpecified true
}

pipeline "build" {

  build "UrlTemplates"
  build "Navs"
  build "Navs.Avalonia"
  build "Navs.FuncUI"
  runIfOnlySpecified false
}

pipeline "nuget:local" {
  build "UrlTemplates"
  build "Navs"
  build "Navs.Avalonia"
  pack "UrlTemplates"
  pack "Navs"
  pack "Navs.Avalonia"
  pack "Navs.FuncUI"
  runIfOnlySpecified true
}

tryPrintPipelineCommandHelp()
