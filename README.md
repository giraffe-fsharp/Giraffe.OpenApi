![Giraffe](https://raw.githubusercontent.com/giraffe-fsharp/Giraffe/master/giraffe.png)

# Giraffe.OpenApi

An extension for the [Giraffe](https://github.com/giraffe-fsharp/Giraffe) Web Application framework with functionality to auto generate OpenApi documentation spec from code.

[![NuGet Info](https://buildstats.info/nuget/Giraffe.OpenApi?includePreReleases=true)](https://www.nuget.org/packages/Giraffe.OpenApi/)


## Table of Contents 

- [About](#about)
- [Getting Started](#getting-started)
- [Documentation](#documentation)
  - [Integration](#integration)
  - [addOpenApi](#addopenapi)
  - [addOpenApiSimple](#addopenapisimple)
  - [configureEndpoint](#configureendpoint)
- [License](#license)

## About

`Giraffe.OpenApi` is a library that extends the `Giraffe` Web Application framework with functionality to auto generate OpenApi documentation spec from code. This means that you can define your API endpoints using Giraffe and generate OpenApi or Swagger documentation from it.

Inspired by the [Oxpecker.OpenApi](https://github.com/Lanayx/Oxpecker/blob/develop/src/Oxpecker.OpenApi) library, but adapted to work with Giraffe.

## Getting Started

Add the `Giraffe.OpenApi` NuGet package to your project:

```bash
dotnet add package Giraffe.OpenApi
```

Two use cases:

```fsharp
open Giraffe
open Giraffe.EndpointRouting
open Giraffe.OpenApi

let endpoints = [
    // addOpenApi supports passing detailed configuration
    POST [
        route "/product" (text "Product posted!")
            |> addOpenApi (OpenApiConfig(
                requestBody = RequestBody(typeof<Product>),
                responseBodies = [| ResponseBody(typeof<string>) |],
                configureOperation = (fun o -> o.OperationId <- "PostProduct"; o)
            ))
    ]
    // addOpenApiSimple is a shortcut for simple cases
    GET [
        routef "/product/{%i}" (
            fun id ->
                forecases
                |> Array.find (fun f -> f.Id = num)
                |> json
        )
            |> configureEndpoint _.WithName("GetProduct")
            |> addOpenApiSimple<int, Product>
    ]
]
```

## Documentation

### Integration

Since `Giraffe.OpenApi` works on top of `Microsoft.AspNetCore.OpenApi` and `Swashbuckle.AspNetCore` packages, you need to do [standard steps](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/openapi):

```fsharp
let configureApp (appBuilder: IApplicationBuilder) =
    appBuilder
        .UseRouting()
        .UseSwagger() // For generating OpenApi spec
        .UseSwaggerUI() // For viewing Swagger UI
        .UseGiraffe(endpoints)
        .UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    services
        .AddRouting()
        .AddGiraffe()
        .AddEndpointsApiExplorer() // Use the API Explorer to discover and describe endpoints
        .AddSwaggerGen() // Swagger dependencies
    |> ignore
```

To make endpoints discoverable by Swagger, you need to call one of the following functions: `addOpenApi` or `addOpenApiSimple` on the endpoint.

_NOTE: you don't have to describe routing parameters when using those functions, they will be inferred from the route template automatically._

### addOpenApi

This method is used to add OpenApi metadata to the endpoint. It accepts `OpenApiConfig` object with the following optional parameters:

```fsharp
type OpenApiConfig (?requestBody : RequestBody,
                    ?responseBodies : ResponseBody seq,
                    ?configureOperation : OpenApiOperation -> OpenApiOperation) =
    // ...
```

Response body schema will be inferred from the types passed to `requestBody` and `responseBodies` parameters. Each `ResponseBody` object in sequence must have different status code.

`configureOperation` parameter is a function that allows you to do very low-level modifications the `OpenApiOperation` object.

### addOpenApiSimple

This method is a shortcut for simple cases. It accepts two generic type parameters - request and response, so the schema can be inferred from them.

```fsharp
let addOpenApiSimple<'Req, 'Res> = ...
```

If your handler doesn't accept any input, you can pass `unit` as a request type (works for response as well).

### configureEndpoint

The two methods above return `Endpoint` object, which can be further configured using `configureEndpoint` method provided by [Giraffe](https://github.com/giraffe-fsharp/Giraffe). It accepts `Endpoint` object and returns the same object, so you can chain multiple calls.

```fsharp
let endpoints = [
    GET [
        route "/hello" (text "Hello, World!")
        |> configureEndpoint _.WithName("HelloWorld")
        |> configureEndpoint _.WithDescription("Simple hello world endpoint")
        |> configureEndpoint _.WithSummary("Hello world")
        |> addOpenApiSimple<unit, string>
    ]
]
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
