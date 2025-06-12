{
  sources ? import ./npins,
  system ? builtins.currentSystem,
  pkgs ? import sources.nixpkgs {
    inherit system;
    config = { };
    overlays = [ ];
  },
}:
let
  pname = "Giraffe.OpenApi";
  dotnet-sdk = pkgs.dotnet-sdk_8;
  dotnet-runtime = pkgs.dotnetCorePackages.runtime_8_0;
  version = "0.0.1";
  shell = pkgs.mkShellNoCC {
    buildInputs = [
      dotnet-sdk
    ];

    packages = [
      pkgs.fantomas
    ];

    DOTNET_ROOT = "${dotnet-sdk}";
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
