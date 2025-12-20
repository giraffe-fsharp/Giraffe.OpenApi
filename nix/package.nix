{
  pname,
  version,
  nix-gitignore,
  dotnet-sdk,
  dotnet-runtime,
  buildDotnetModule,
}:
buildDotnetModule {
  inherit
    pname
    version
    dotnet-sdk
    dotnet-runtime
    ;
  name = pname;
  src = nix-gitignore.gitignoreSource [ ] ../.;
  projectFile = "src/Giraffe.OpenApi/Giraffe.OpenApi.fsproj";
  nugetDeps = ./deps.json;
  doCheck = false;
}
