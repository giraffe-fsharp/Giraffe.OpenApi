{
  sources ? import ./deps,
  system ? builtins.currentSystem,
  pkgs ? import sources.nixpkgs { inherit system; config = {}; overlays = []; },
}: {
  shell = pkgs.mkShell {
    name = "Giraffe.OpenApi";

    packages = with pkgs; [
      dotnet-sdk_8
    ];

    DOTNET_ROOT = "${pkgs.dotnet-sdk_8}";
  };
}

