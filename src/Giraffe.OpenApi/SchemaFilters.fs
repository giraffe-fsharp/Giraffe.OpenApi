namespace Giraffe.OpenApi

open System
open Microsoft.FSharp.Reflection
open Microsoft.OpenApi.Any
open Microsoft.OpenApi.Models
open Swashbuckle.AspNetCore.SwaggerGen

/// <summary>
/// Schema filter that maps F# discriminated unions and regular .NET enums to OpenAPI string enums.
/// For DUs: Only applies to simple discriminated unions (no fields on union cases).
/// For .NET enums: Converts all enum values to camelCase strings.
/// </summary>
type DiscriminatedUnionSchemaFilter() =
    interface ISchemaFilter with
        member _.Apply(schema: OpenApiSchema, context: SchemaFilterContext) =
            // Check if the type is a regular .NET enum
            if context.Type.IsEnum then
                // Map to string enum
                schema.Type <- "string"
                schema.Format <- null

                // Get enum names and convert to camelCase
                let enumNames = Enum.GetNames(context.Type)
                let enumValues =
                    enumNames
                    |> Array.map (fun name ->
                        let camelCase =
                            if name.Length > 0 then
                                let firstChar = Char.ToLowerInvariant(name.[0])
                                firstChar.ToString() + name.Substring(1)
                            else
                                name
                        OpenApiString(camelCase) :> IOpenApiAny
                    )

                schema.Enum <- ResizeArray(enumValues)

                // Add description showing the enum values
                let enumList = String.Join(", ", enumNames)
                schema.Description <-
                    match schema.Description with
                    | null | "" -> sprintf "Enum values: %s" enumList
                    | existing -> sprintf "%s (Enum values: %s)" existing enumList

            // Check if the type is an F# discriminated union
            elif FSharpType.IsUnion(context.Type) then
                // Exclude option types and unit type
                let isOption = context.Type.IsGenericType && context.Type.GetGenericTypeDefinition() = typedefof<option<_>>
                let isUnit = context.Type = typeof<unit>

                if not isOption && not isUnit then
                    let cases = FSharpType.GetUnionCases(context.Type)

                    // Only process simple DUs (no fields on union cases)
                    let isSimpleDU = cases |> Array.forall (fun case -> case.GetFields().Length = 0)

                    if isSimpleDU then
                        // Map to string enum (even for DUs with = values like "| Low = 0")
                        schema.Type <- "string"
                        schema.Properties <- null
                        schema.Required <- null
                        schema.Format <- null // Clear format if it was set to int32

                        // Convert union case names to camelCase for the enum values
                        let enumValues =
                            cases
                            |> Array.map (fun case ->
                                let caseName =
                                    // Convert PascalCase to camelCase
                                    if case.Name.Length > 0 then
                                        let firstChar = Char.ToLowerInvariant(case.Name.[0])
                                        firstChar.ToString() + case.Name.Substring(1)
                                    else
                                        case.Name
                                OpenApiString(caseName) :> IOpenApiAny
                            )

                        schema.Enum <- ResizeArray(enumValues)

                        // Add description showing the enum values
                        let enumList = String.Join(", ", cases |> Array.map (fun c -> c.Name))
                        schema.Description <-
                            match schema.Description with
                            | null | "" -> sprintf "Enum values: %s" enumList
                            | existing -> sprintf "%s (Enum values: %s)" existing enumList
