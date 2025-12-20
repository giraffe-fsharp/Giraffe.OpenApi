namespace Giraffe.OpenApi

// Modified from https://github.com/Lanayx/Oxpecker/blob/develop/src/Oxpecker.OpenApi/Transformers.fs.
//
// MIT License
//
// Copyright (c) 2025 Vladimir Shchur
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

open System
open System.Collections.Generic
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.OpenApi
open Microsoft.FSharp.Reflection
open Microsoft.OpenApi

module private Helpers =
    let strNull = String.IsNullOrWhiteSpace

    let (|FSharpOptionKind|_|) (t: Type) =
        if t.IsGenericType then
            let gtd = t.GetGenericTypeDefinition()
            if gtd = typedefof<option<_>> || gtd = typedefof<ValueOption<_>> then
                Some(t.GetGenericArguments()[0])
            else
                None
        else
            None

    let (|FSharpUnionKind|_|) (t: Type) =
        if FSharpType.IsUnion(t) then
            let isOption =
                t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>
            let isValueOption =
                t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<ValueOption<_>>
            // NOTE: Not sure if needed
            let isUnit = t = typeof<unit>

            if not isOption && not isValueOption && not isUnit then
                Some(FSharpType.GetUnionCases(t))
            else
                None
        else
            None

    /// Shallowly "adopt" the public, settable surface from `src` into `dst`.
    /// We intentionally avoid touching internals; only copy what's publicly available.
    let copyTo (dst: OpenApiSchema) (src: IOpenApiSchema) =
        // Json-Schema identity & metadata
        dst.Title <- src.Title
        dst.Schema <- src.Schema
        dst.Id <- src.Id
        dst.Comment <- src.Comment
        dst.Vocabulary <-
            match src.Vocabulary with
            | null -> null
            | v -> Dictionary(v)
        dst.DynamicRef <- src.DynamicRef
        dst.DynamicAnchor <- src.DynamicAnchor
        dst.Definitions <-
            match src.Definitions with
            | null -> null
            | d -> Dictionary(d)
        // Numeric/string constraints
        dst.ExclusiveMaximum <- src.ExclusiveMaximum
        dst.ExclusiveMinimum <- src.ExclusiveMinimum
        dst.Maximum <- src.Maximum
        dst.Minimum <- src.Minimum
        dst.MultipleOf <- src.MultipleOf
        dst.MaxLength <- src.MaxLength
        dst.MinLength <- src.MinLength
        dst.Pattern <- src.Pattern

        // Type/format & const/default
        dst.Type <- src.Type
        dst.Format <- src.Format
        dst.Const <- src.Const
        dst.Default <- src.Default

        // Read/Write/Deprecated
        dst.ReadOnly <- src.ReadOnly
        dst.WriteOnly <- src.WriteOnly
        dst.Deprecated <- src.Deprecated

        // Compositions & negation
        dst.AllOf <-
            match src.AllOf with
            | null -> null
            | a -> ResizeArray(a)
        dst.AnyOf <-
            match src.AnyOf with
            | null -> null
            | a -> ResizeArray(a)
        dst.OneOf <-
            match src.OneOf with
            | null -> null
            | a -> ResizeArray(a)
        dst.Not <- src.Not

        // Array/object facets
        dst.Items <- src.Items
        dst.MaxItems <- src.MaxItems
        dst.MinItems <- src.MinItems
        dst.UniqueItems <- src.UniqueItems

        dst.Properties <-
            match src.Properties with
            | null -> null
            | p -> Dictionary(p)
        dst.PatternProperties <-
            match src.PatternProperties with
            | null -> null
            | p -> Dictionary(p)
        dst.MaxProperties <- src.MaxProperties
        dst.MinProperties <- src.MinProperties
        dst.Required <-
            match src.Required with
            | null -> null
            | r -> HashSet(r)
        dst.AdditionalPropertiesAllowed <- src.AdditionalPropertiesAllowed
        dst.AdditionalProperties <- src.AdditionalProperties

        // Misc
        dst.Discriminator <- src.Discriminator
        dst.Description <- src.Description
        dst.Example <- src.Example
        dst.Examples <-
            match src.Examples with
            | null -> null
            | e -> ResizeArray(e)
        dst.Enum <-
            match src.Enum with
            | null -> null
            | e -> ResizeArray(e)
        dst.UnevaluatedProperties <- src.UnevaluatedProperties
        dst.ExternalDocs <- src.ExternalDocs
        dst.Xml <- src.Xml
        dst.Extensions <-
            match src.Extensions with
            | null -> null
            | e -> Dictionary(e)
        dst.UnrecognizedKeywords <-
            match src.UnrecognizedKeywords with
            | null -> null
            | e -> Dictionary(e)
        dst.Metadata <-
            if src :? IMetadataContainer then
                (src :?> IMetadataContainer).Metadata
            else
                null
        dst.DependentRequired <-
            match src.DependentRequired with
            | null -> null
            | d -> Dictionary(d)

    /// Union an existing JsonSchemaType with `null` (OpenAPI 3.1), and also drives `nullable: true` for 3.0.
    let unionWithNull (t: Nullable<JsonSchemaType>) : Nullable<JsonSchemaType> =
        if t.HasValue then
            let combined =
                LanguagePrimitives.EnumOfValue((int t.Value) ||| (int JsonSchemaType.Null))
            Nullable<JsonSchemaType>(combined)
        else
            // Leave as null; writer will omit 'type'.
            Nullable()

    let toCamelCase (str: string) =
        if strNull str then
            str
        else
            let firstChar = Char.ToLowerInvariant(str.[0])
            firstChar.ToString() + str.Substring(1)

type FSharpOptionSchemaTransformer() =
    interface IOpenApiSchemaTransformer with
        member _.TransformAsync
            (schema: OpenApiSchema, ctx: OpenApiSchemaTransformerContext, ct: CancellationToken)
            : Task
            =
            task {
                match ctx.JsonTypeInfo.Type with
                | Helpers.FSharpOptionKind innerT ->
                    // Ask pipeline for T's schema …
                    let! inner = ctx.GetOrCreateSchemaAsync(innerT, ctx.ParameterDescription, ct)
                    // … copy its shape …
                    inner |> Helpers.copyTo schema
                    // … and mark nullable (OAS 3.0 => "nullable: true"; OAS 3.1 => type union with "null")
                    schema.Type <- Helpers.unionWithNull schema.Type
                | _ -> ()
            }
            :> Task

/// <summary>
/// Schema transformer that maps F# discriminated unions and regular .NET enums to OpenAPI string enums.
/// For DUs: Only applies to simple discriminated unions (no fields on union cases).
/// For .NET enums: Converts all enum values to camelCase strings.
/// </summary>
type DiscriminatedUnionSchemaTransformer() =
    interface IOpenApiSchemaTransformer with
        member _.TransformAsync
            (schema: OpenApiSchema, ctx: OpenApiSchemaTransformerContext, ct: CancellationToken)
            : Task
            =
            task {
                let t = ctx.JsonTypeInfo.Type

                // Check if the type is a regular .NET enum
                if t.IsEnum then
                    // Map to string enum
                    schema.Type <- JsonSchemaType.String
                    schema.Format <- null

                    // Get enum names and convert to camelCase
                    let enumNames = Enum.GetNames(t)
                    let enumValues =
                        enumNames
                        |> Array.map (fun name ->
                            JsonValue.Create(Helpers.toCamelCase name) :> JsonNode
                        )

                    schema.Enum <- ResizeArray(enumValues)

                    // Add description showing the enum values
                    let enumList = String.Join(" | ", enumNames)
                    schema.Description <-
                        match schema.Description with
                        | null
                        | "" -> $"Enum values: %s{enumList}"
                        | existing -> $"%s{existing} (Enum values: %s{enumList})"

                // Check if the type is an F# discriminated union
                else
                    match t with
                    | Helpers.FSharpUnionKind cases ->
                        // Only process simple DUs (no fields on union cases)
                        let isSimpleDU =
                            cases |> Array.forall (fun case -> case.GetFields().Length = 0)

                        if isSimpleDU then
                            // Map to string enum
                            schema.Type <- JsonSchemaType.String
                            schema.Properties <- null
                            schema.Required <- null
                            schema.Format <- null

                            // Convert union case names to camelCase for the enum values
                            let enumValues =
                                cases
                                |> Array.map (fun case ->
                                    JsonValue.Create(Helpers.toCamelCase case.Name) :> JsonNode
                                )

                            schema.Enum <- ResizeArray(enumValues)

                            // Add description showing the enum values
                            let enumList = String.Join(" | ", cases |> Array.map (fun c -> c.Name))
                            schema.Description <-
                                match schema.Description with
                                | null
                                | "" -> $"Enum values: %s{enumList}"
                                | existing -> $"%s{existing} (Enum values: %s{enumList})"
                    | _ -> ()
            }
            :> Task
