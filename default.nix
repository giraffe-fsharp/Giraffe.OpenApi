{
  sources ? import ./nix,
  pkgs ? import sources.nixpkgs { },
}:
let
  pname = "Giraffe.OpenApi";
  dotnet-sdk = pkgs.dotnetCorePackages.sdk_9_0;
  dotnet-runtime = pkgs.dotnetCorePackages.aspnetcore_9_0;
  version = "0.0.3";
  shell = pkgs.mkShellNoCC {
    buildInputs = [
      dotnet-sdk
    ];

    packages = [
      pkgs.npins
      pkgs.fantomas
      pkgs.fsautocomplete
    ];

    DOTNET_ROOT = "${dotnet-sdk.unwrapped}/share/dotnet";
    DOTNET_CLI_TELEMETRY_OPTOUT = "true";
    NPINS_DIRECTORY = "nix";
  };
in
{
  inherit shell;

  default = pkgs.callPackage ./nix/package.nix {
    inherit
      pname
      version
      dotnet-sdk
      dotnet-runtime
      pkgs
      ;
  };
}
