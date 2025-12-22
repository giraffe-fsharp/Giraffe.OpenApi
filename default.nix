{
  sources ? import ./nix,
  pkgs ? import sources.nixpkgs { },
}:
let
  pname = "Giraffe.OpenApi";
  dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
  dotnet-runtime = pkgs.dotnetCorePackages.aspnetcore_10_0;
  version = "0.1.0";
  fsharp-analyzers = pkgs.buildDotnetGlobalTool {
    pname = "fsharp-analyzers";
    version = "0.34.1 ";
    nugetHash = "sha256-Y6PzfVGob2EgX29ZhZIde5EhiZ28Y1+U2pJ6ybIsHV0=";
  };
in
{
  default = pkgs.callPackage ./nix/package.nix {
    inherit
      pname
      version
      dotnet-sdk
      dotnet-runtime
      ;
  };
  example = pkgs.callPackage ./nix/example.nix {
    inherit
      version
      dotnet-sdk
      dotnet-runtime
      ;
  };

  shell = pkgs.mkShellNoCC {
    buildInputs = [
      dotnet-sdk
    ];

    packages = [
      pkgs.npins
      fsharp-analyzers
      pkgs.fantomas
      pkgs.fsautocomplete
    ];

    DOTNET_ROOT = "${dotnet-sdk}/share/dotnet";
    NPINS_DIRECTORY = "nix";
  };
}
