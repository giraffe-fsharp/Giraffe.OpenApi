open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.OpenApi.Models
open Giraffe
open Giraffe.EndpointRouting
open Giraffe.OpenApi

/// <summary>
/// Fsharp Message type
/// </summary>
type FsharpMessage = {
    /// <summary>
    /// Hello content
    /// </summary>
    /// <example>This is an Example</example>
    Hello: string
}

let handler1 (_: HttpFunc) (ctx: HttpContext) = ctx.WriteTextAsync "Hello World"

let handler2 (firstName: string, age: int) (_: HttpFunc) (ctx: HttpContext) =
    $"Hello %s{firstName}, you are %i{age} years old." |> ctx.WriteTextAsync

let endpoints = [
    GET [
        route "/hello" (json { Hello = "Hello from Giraffe" })
        |> configureEndpoint _.WithTags("SampleApp")
        |> configureEndpoint _.WithSummary("Fetches a Hello from Giraffe")
        |> configureEndpoint _.WithDescription("Will return a Hello from Giraffe.")
        |> addOpenApiSimple<unit, FsharpMessage>

        routef "/%s/%i" handler2
        |> configureEndpoint _.WithTags("SampleApp")
        |> configureEndpoint _.WithSummary("Fetches a response from handler2")
        |> configureEndpoint _.WithDescription("Will return a Hello from Handler 2.")
        |> addOpenApiSimple<string * int, string>
    ]
    POST [
        route "/message" (text "Message posted!")
        |> configureEndpoint _.WithSummary("Posts a message")
        |> configureEndpoint _.WithDescription("Will return a message posted")
        |> addOpenApi (
            OpenApiConfig(
                requestBody = RequestBody(typeof<FsharpMessage>),
                responseBodies = [| ResponseBody(typeof<string>) |],
                configureOperation =
                    (fun o ->
                        o.OperationId <- "PostMessage"
                        o
                    )
            )
        )
    ]
]

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureApp (appBuilder: IApplicationBuilder) =
    appBuilder
        .UseRouting()
        .UseSwagger() // For generating OpenApi spec
        .UseSwaggerUI() // For viewing Swagger UI
        .UseGiraffe(endpoints)
        .UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    let openApiInfo = OpenApiInfo() // Configure OpenApi
    openApiInfo.Description <- "Documentation for my API"
    openApiInfo.Title <- "My API"
    openApiInfo.Version <- "v1"
    openApiInfo.Contact <- OpenApiContact()
    openApiInfo.Contact.Name <- "Joe Developer"
    openApiInfo.Contact.Email <- "joe.developer@tempuri.org"

    services
        .AddRouting()
        .AddGiraffe()
        .AddEndpointsApiExplorer() // Use the API Explorer to discover and describe endpoints
        .AddSwaggerGen(fun opt ->
            opt.SwaggerDoc("v1", openApiInfo)
            let xmlPath = Path.Combine(AppContext.BaseDirectory, "SampleApp.xml")
            opt.IncludeXmlComments(xmlPath)
            opt.SupportNonNullableReferenceTypes()
        ) // Swagger dependencies
    |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services

    let app = builder.Build()

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    configureApp app
    app.Run()

    0
