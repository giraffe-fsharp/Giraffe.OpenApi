{
  sources ? import ./deps,
  system ? builtins.currentSystem,
}:
(import ./. {inherit sources system;}).shell
