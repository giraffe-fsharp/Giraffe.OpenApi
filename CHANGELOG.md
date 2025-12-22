Notable changes are recorded here.

# Giraffe.OpenApi 0.1.0

## Breaking Changes

- Upgrade to .NET 10.0
- Update Microsoft.AspNetCore.OpenApi from 8.0.20 to 10.0.0 (not backwards compatible)

## Updates

- Update Giraffe from 8.0.0-alpha-003 to 8.2.0
- Update FSharp.SystemTextJson from 1.3.13 to 1.4.36

# Giraffe.OpenApi 0.0.3

## Updates

- Add support for discriminated unions - Credits @OnurGumus

# Giraffe.OpenApi 0.0.2

## Updates

- Add labeled path parameters - Credits @RJSonnenberg
- Fix issue where using multiple path parameters AND addOpenApiSimple, which requires a tuple for the request type, caused a request body to be required despite all parameters being in path. - Credits @RJSonnenberg
- Update sample project to redirect to Swagger UI upon startup. - Credits @RJSonnenberg

# Giraffe.OpenApi 0.0.1

Initial Version
