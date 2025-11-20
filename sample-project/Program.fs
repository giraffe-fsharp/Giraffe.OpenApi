open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
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

/// <summary>
/// Example discriminated union for media set status
/// </summary>
type MediaSetStatus =
    | Pending
    | Completed
    | Rejected
    | Expired

/// <summary>
/// Example regular .NET enum for priority level
/// </summary>
type PriorityLevel =
    | Low = 0
    | Medium = 1
    | High = 2
    | Critical = 3

/// <summary>
/// Media set status information
/// </summary>
type MediaSetStatusInfo = {
    /// <summary>
    /// Media set ID
    /// </summary>
    MediaSetId: string
    /// <summary>
    /// Current status of the media set
    /// </summary>
    Status: MediaSetStatus
    /// <summary>
    /// When the media set was activated
    /// </summary>
    ActivationTime: DateTime
    /// <summary>
    /// Last update time
    /// </summary>
    UpdateTime: DateTime
    /// <summary>
    /// Number of retries
    /// </summary>
    RetryCount: int
    /// <summary>
    /// Processing priority number
    /// </summary>
    Priority: int
    /// <summary>
    /// Priority level (regular .NET enum)
    /// </summary>
    PriorityLevel: PriorityLevel
}

let handler1 (_: HttpFunc) (ctx: HttpContext) = ctx.WriteTextAsync "Hello World"

let handler2 (firstName: string, age: int) (_: HttpFunc) (ctx: HttpContext) =
    $"Hello %s{firstName}, you are %i{age} years old." |> ctx.WriteTextAsync

let handler3 (firstName: string) (_: HttpFunc) (ctx: HttpContext) =
    $"Hello %s{firstName}!" |> ctx.WriteTextAsync

let mediaSetStatusHandler (next: HttpFunc) (ctx: HttpContext) =
    let statusInfo = [|
        {
            MediaSetId = "media-123"
            Status = Pending
            ActivationTime = DateTime.UtcNow
            UpdateTime = DateTime.UtcNow
            RetryCount = 0
            Priority = 1
            PriorityLevel = PriorityLevel.Low
        }
        {
            MediaSetId = "media-456"
            Status = Completed
            ActivationTime = DateTime.UtcNow.AddHours(-2.0)
            UpdateTime = DateTime.UtcNow.AddHours(-1.0)
            RetryCount = 2
            Priority = 5
            PriorityLevel = PriorityLevel.Critical
        }
    |]
    json statusInfo next ctx

/// Redirects to the swagger interface from the root of the site.
let swaggerRedirectHandler: HttpHandler = redirectTo true "swagger/index.html"

let endpoints = [
    route "/" swaggerRedirectHandler
    GET [
        route "/hello" (json { Hello = "Hello from Giraffe" })
        |> configureEndpoint _.WithTags("SampleApp")
        |> configureEndpoint _.WithSummary("Fetches a Hello from Giraffe")
        |> configureEndpoint _.WithDescription("Will return a Hello from Giraffe.")
        |> addOpenApiSimple<unit, FsharpMessage>

        routef "first-names/%s:firstName/ages/%i:age" handler2
        |> configureEndpoint _.WithTags("SampleApp")
        |> configureEndpoint _.WithSummary("Fetches a response from handler2")
        |> configureEndpoint _.WithDescription("Will return a Hello from Handler 2.")
        |> addOpenApiSimple<string * int, string>

        routef "names/%s:firstName" handler3
        |> configureEndpoint _.WithTags("SampleApp")
        |> configureEndpoint _.WithSummary("Fetches a response from handler3")
        |> configureEndpoint _.WithDescription("Will return a Hello from Handler 3.")
        |> addOpenApiSimple<string, string>

        route "/media-sets/status" mediaSetStatusHandler
        |> configureEndpoint _.WithTags("MediaSet")
        |> configureEndpoint _.WithSummary("Gets media set status information")
        |> configureEndpoint _.WithDescription("Returns an array of media set status info with discriminated union status field.")
        |> addOpenApiSimple<unit, MediaSetStatusInfo array>
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

    // Configure JSON serialization options with F# support
    let fsOptions =
        JsonFSharpOptions.Default()
            .WithUnionAdjacentTag()
            .WithUnionTagCaseInsensitive()
            .WithUnionUnwrapFieldlessTags()
            .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)

    let jsonOptions = JsonSerializerOptions()
    jsonOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    jsonOptions.Converters.Add(JsonStringEnumConverter(JsonNamingPolicy.CamelCase))

    // Configure ASP.NET Core JSON options (for OpenAPI schema generation)
    services.ConfigureHttpJsonOptions(fun (options: Microsoft.AspNetCore.Http.Json.JsonOptions) ->
        options.SerializerOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        options.SerializerOptions.Converters.Add(JsonStringEnumConverter(JsonNamingPolicy.CamelCase))
        let fsOpts =
            JsonFSharpOptions.Default()
                .WithUnionAdjacentTag()
                .WithUnionTagCaseInsensitive()
                .WithUnionUnwrapFieldlessTags()
                .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)
        options.SerializerOptions.Converters.Add(JsonFSharpConverter(fsOpts))
    ) |> ignore

    services
        .AddRouting()
        .AddSingleton<Json.ISerializer>(Json.FsharpFriendlySerializer(fsOptions, jsonOptions))
        .AddGiraffe()
        .AddEndpointsApiExplorer() // Use the API Explorer to discover and describe endpoints
        .AddSwaggerGen(fun opt ->
            opt.SwaggerDoc("v1", openApiInfo)
            let xmlPath = Path.Combine(AppContext.BaseDirectory, "SampleApp.xml")
            opt.IncludeXmlComments(xmlPath)
            opt.SupportNonNullableReferenceTypes()
            // Register the discriminated union schema filter
            opt.SchemaFilter<DiscriminatedUnionSchemaFilter>()
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
