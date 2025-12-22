open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting
open Giraffe.OpenApi
open Scalar.AspNetCore
open System.ComponentModel.DataAnnotations

/// <summary>
/// Fsharp Message type
/// </summary>
[<CLIMutable>]
type FsharpMessage = {
    /// <summary>
    /// Hello content
    /// </summary>
    /// <example>This is an Example</example>
    [<JsonRequired>]
    Hello: string
    /// <summary>
    /// Age content
    /// </summary>
    Age: int option
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

let handler1 (next: HttpFunc) (ctx: HttpContext) =
    json { Hello = "Hello from Giraffe"; Age = None } next ctx

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

let messagePostHandler (next: HttpFunc) (ctx: HttpContext) =
    task {
        let! message = ctx.BindJsonAsync<FsharpMessage>()
        return! ctx.WriteTextAsync($"Message posted: %s{message.Hello}")
    }

/// Redirects to the scalar interface from the root of the site.
let scalarRedirectHandler: HttpHandler = redirectTo true "/scalar/v1"

let endpoints = [
    route "/" scalarRedirectHandler
    GET [
        route "/hello" handler1
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
        |> configureEndpoint
            _.WithDescription(
                "Returns an array of media set status info with discriminated union status field."
            )
        |> addOpenApiSimple<unit, MediaSetStatusInfo array>
    ]
    POST [
        route "/message" messagePostHandler
        |> configureEndpoint _.WithTags("SampleApp")
        |> configureEndpoint _.WithSummary("Posts a message")
        |> configureEndpoint _.WithDescription("Will return a message posted confirmation")
        |> addOpenApi (
            OpenApiConfig(
                requestBody = RequestBody(typeof<FsharpMessage>),
                responseBodies = [| ResponseBody(typeof<string>) |]
            )
        )
    ]
]

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureApp (appBuilder: IApplicationBuilder) =
    appBuilder.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    // Configure JSON serialization options with F# support
    let fsOptions =
        JsonFSharpOptions
            .Default()
            .WithUnionAdjacentTag()
            .WithUnionTagCaseInsensitive()
            .WithUnionUnwrapFieldlessTags()
            .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)

    let jsonOptions = JsonSerializerOptions()
    jsonOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    jsonOptions.Converters.Add(JsonStringEnumConverter(JsonNamingPolicy.CamelCase))
    jsonOptions.Converters.Add(JsonFSharpConverter(fsOptions))

    services
        .AddRouting()
        .AddSingleton<Json.ISerializer>(Json.FsharpFriendlySerializer(fsOptions, jsonOptions))
        .AddGiraffe()
        .AddOpenApi(
            "v1",
            fun (options: Microsoft.AspNetCore.OpenApi.OpenApiOptions) ->
                // Configure OpenAPI document metadata
                options.AddDocumentTransformer(fun
                                                   (document: Microsoft.OpenApi.OpenApiDocument)
                                                   (context:
                                                       Microsoft.AspNetCore.OpenApi.OpenApiDocumentTransformerContext)
                                                   (ct: System.Threading.CancellationToken) ->
                    document.Info <-
                        Microsoft.OpenApi.OpenApiInfo(
                            Title = "Giraffe OpenAPI Sample API",
                            Version = "v1.0.0",
                            Description =
                                "A sample API demonstrating Giraffe with OpenAPI and Scalar documentation",
                            Contact =
                                Microsoft.OpenApi.OpenApiContact(
                                    Name = "API Support",
                                    Email = "support@example.com",
                                    Url = Uri("https://example.com/support")
                                ),
                            License =
                                Microsoft.OpenApi.OpenApiLicense(
                                    Name = "MIT",
                                    Url = Uri("https://opensource.org/licenses/MIT")
                                )
                        )

                    // Add server information
                    document.Servers <-
                        ResizeArray(
                            [
                                Microsoft.OpenApi.OpenApiServer(
                                    Url = "http://localhost:5000",
                                    Description = "Development server"
                                )
                                Microsoft.OpenApi.OpenApiServer(
                                    Url = "https://api.example.com",
                                    Description = "Production server"
                                )
                            ]
                        )

                    Task.CompletedTask
                )
                |> ignore

                // Register F# option and discriminated union schema transformers
                options.AddSchemaTransformer<FSharpOptionSchemaTransformer>() |> ignore
                options.AddSchemaTransformer<DiscriminatedUnionSchemaTransformer>() |> ignore
        ) // Add OpenAPI support with F# transformers
    |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services

    let app = builder.Build()

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    // Map OpenAPI endpoint
    app.MapOpenApi() |> ignore

    configureApp app

    // Map Scalar API Reference UI
    app.MapScalarApiReference(fun (options: Scalar.AspNetCore.ScalarOptions) ->
        options.WithTitle("Giraffe OpenAPI Sample API") |> ignore
        options.WithTheme(ScalarTheme.Purple) |> ignore
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        |> ignore
        options.WithDarkMode(true) |> ignore
        options.WithSidebar(true) |> ignore
        options.WithModels(true) |> ignore
        options.WithSearchHotKey("k") |> ignore
    )
    |> ignore

    app.Run()

    0
