{
  dotnet-sdk,
  nix-gitignore,
  dotnet-runtime,
  buildDotnetModule,
  version,
}:
let
  name = "Example";
in
buildDotnetModule {
  pname = name;
  inherit dotnet-sdk dotnet-runtime version;

  src = nix-gitignore.gitignoreSource [ ] ../.;
  projectFile = "example/SampleApp.fsproj";
  nugetDeps = ./example-deps.json;

  doCheck = false;
}
