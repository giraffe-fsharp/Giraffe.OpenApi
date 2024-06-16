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

let handler1: HttpHandler =
    fun (_: HttpFunc) (ctx: HttpContext) -> ctx.WriteTextAsync "Hello World"

let handler2 (firstName: string, age: int) : HttpHandler =
    fun (_: HttpFunc) (ctx: HttpContext) ->
        sprintf "Hello %s, you are %i years old." firstName age |> ctx.WriteTextAsync

let endpoints = [
      GET [
            route "/hello" (json {Hello = "Hello from Giraffe"})
            |> configureEndpoint _.WithTags("helloGiraffe")
            |> configureEndpoint _.WithSummary("Fetches a Hello from Giraffe")
            |> configureEndpoint _.WithDescription("Will return a Hello from Giraffe.")
            |> addOpenApiSimple<unit, FsharpMessage>
            
            routef "/%s/%i" handler2
            |> configureEndpoint _.WithTags("handler2")
            |> configureEndpoint _.WithSummary("Fetches a response from handler2")
            |> configureEndpoint _.WithDescription("Will return a Hello from Handler 2.")
            |> addOpenApiSimple<(string * int), string>
      ]
]
      
let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureApp (appBuilder: IApplicationBuilder) =
    appBuilder
        .UseRouting()
        .UseSwagger() // for generating OpenApi spec
        .UseSwaggerUI() // for viewing Swagger UI
        .UseGiraffe(endpoints)
        .UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    // Configure OpenApi
    let openApiInfo = OpenApiInfo()
    openApiInfo.Description <- "Documentation for my API"
    openApiInfo.Title <- "My API"
    openApiInfo.Version <- "v1"
    openApiInfo.Contact <- OpenApiContact()
    openApiInfo.Contact.Name <- "Joe Developer"
    openApiInfo.Contact.Email <- "joe.developer@tempuri.org"

    services
        .AddRouting()
        .AddGiraffe()
        .AddEndpointsApiExplorer() // use the API Explorer to discover and describe endpoints
        .AddSwaggerGen(fun opt ->
            opt.SwaggerDoc("v1", openApiInfo)
            let xmlPath = Path.Combine(AppContext.BaseDirectory, "EndpointRoutingApp.xml")
            opt.IncludeXmlComments(xmlPath)
            opt.SupportNonNullableReferenceTypes()
        )// swagger dependencies
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
