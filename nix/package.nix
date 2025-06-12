{
  lib,
  pname,
  version,
  dotnet-sdk,
  dotnet-runtime,
  pkgs,
}:
pkgs.buildDotnetModule {
  inherit
    pname
    version
    dotnet-sdk
    dotnet-runtime
    ;
  name = pname;
  src = lib.cleanSource ../.;
  projectFile = "src/Giraffe.OpenApi/Giraffe.OpenApi.fsproj";
  nugetDeps = ./deps.json;
  doCheck = true;
}
