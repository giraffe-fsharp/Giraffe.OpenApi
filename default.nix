{
  sources ? import ./deps,
  system ? builtins.currentSystem,
}: let
  pname = "GiraffeOpenApi";
  dotnet-sdk = pkgs.dotnet-sdk_8;
  dotnet-runtime = pkgs.dotnetCorePackages.runtime_8_0;
  version = "0.0.1";
  dotnetTool = toolName: toolVersion: sha256:
    pkgs.stdenvNoCC.mkDerivation rec {
      name = toolName;
      version = toolVersion;
      nativeBuildInputs = [pkgs.makeWrapper];
      src = pkgs.fetchNuGet {
        pname = name;
        version = version;
        sha256 = sha256;
        installPhase = ''mkdir -p $out/bin && cp -r tools/net8.0/any/* $out/bin'';
      };
      installPhase = ''
        runHook preInstall
        mkdir -p "$out/lib"
        cp -r ./bin/* "$out/lib"
        makeWrapper "${dotnet-runtime}/bin/dotnet" "$out/bin/${name}" --add-flags "$out/lib/${name}.dll"
        runHook postInstall
      '';
    };
  shell = pkgs.mkShell {
    nativeBuildInputs = [
      dotnet-sdk
    ];

    DOTNET_ROOT = "${dotnet-sdk}";
  };
  pkgs = import sources.nixpkgs {
    inherit system;
    config = {};
    overlays = [];
  };
in {
  inherit shell;
  fantomas = dotnetTool "fantomas" (builtins.fromJSON (builtins.readFile ./.config/dotnet-tools.json)).tools.fantomas.version (builtins.head (builtins.filter (elem: elem.pname == "fantomas") ((import ./deps/deps.nix) {fetchNuGet = x: x;}))).sha256;
  default = pkgs.callPackage ./deps/giraffe-openapi.nix {
    inherit pname version dotnet-sdk dotnet-runtime pkgs;
  };
}
