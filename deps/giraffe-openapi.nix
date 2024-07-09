{
  pname,
  version,
  dotnet-sdk,
  dotnet-runtime,
  pkgs,
}:
pkgs.buildDotnetModule rec {
  inherit pname version dotnet-sdk dotnet-runtime;
  name = "Giraffe.OpenApi";
  src = ../.;
  nugetDeps = ./deps.nix;
  doCheck = false;
}
