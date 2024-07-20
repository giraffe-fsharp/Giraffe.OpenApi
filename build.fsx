#r "nuget: Fun.Build, 1.0.4"
#r "nuget: Fake.IO.FileSystem, 6.0.0"

open System
open System.IO
open Fake.IO
open Fake.IO.FileSystemOperators
open Fun.Build

let solutionFile = "Giraffe.OpenApi.sln"

let apiKey = Environment.GetEnvironmentVariable "GIRAFFE_OPENAPI_NUGET_KEY"
let packageOutput = __SOURCE_DIRECTORY__ </> "artifacts" </> "package" </> "release"

pipeline "Build" {
    workingDir __SOURCE_DIRECTORY__
    stage "clean" {
        run (fun _ ->
            async {
                let deleteIfExists folder =
                    if Directory.Exists folder then
                        Directory.Delete(folder, true)

                deleteIfExists packageOutput
                deleteIfExists (__SOURCE_DIRECTORY__ </> "output")
                deleteIfExists (__SOURCE_DIRECTORY__ </> "docs" </> ".tool" </> "dist")
                return 0
            }
        )
    }
    stage "lint" {
        run "dotnet tool restore"
        run "dotnet fantomas . --check"
    }
    stage "restore" { run "dotnet restore -tl" }
    stage "build" { run "dotnet build --no-restore -c Release ./Giraffe.OpenApi.sln -tl" }
    stage "test" { run "dotnet test --no-restore --no-build -c Release -tl" }
    stage "pack" { run "dotnet pack ./src/Giraffe.OpenApi/Giraffe.OpenApi.fsproj -c Release -tl" }
    // stage "docs" {
    //     stage "client" {
    //         workingDir "tool/client"
    //         run "bun i --frozen-lockfile"
    //         run "bunx --bun vite build"
    //     }
    //     run (fun _ -> Shell.copyRecursive "./tool/client/dist" "./docs" true |> ignore)
    //     run "dotnet fsdocs build --noapidocs"
    // }
    stage "push" {
        whenCmdArg "--push"
        workingDir packageOutput
        run
            $"dotnet nuget push Giraffe.OpenApi.*.nupkg --source https://api.nuget.org/v3/index.json --api-key {apiKey} --skip-duplicate"
    }
    runIfOnlySpecified false
}

pipeline "Analyze" {
    workingDir __SOURCE_DIRECTORY__
    stage "Report" { run $"dotnet msbuild /t:AnalyzeSolution %s{solutionFile}" }
    runIfOnlySpecified true
}

pipeline "Publish" {
    workingDir __SOURCE_DIRECTORY__
    stage "publish" {
        run
            "dotnet publish --nologo -c Release --ucr -p:PublishReadyToRun=true ./src/Giraffe.OpenApi/Giraffe.OpenApi.fsproj"
    }
    runIfOnlySpecified true
}

tryPrintPipelineCommandHelp ()
