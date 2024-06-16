namespace Giraffe.OpenApi

// Modified for Giraffe, from https://github.com/Lanayx/Oxpecker/blob/develop/src/Oxpecker.OpenApi/Routing.fs.
// 
// MIT License
//
// Copyright (c) 2023 Vladimir Shchur
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

open System.Reflection

[<AutoOpen>]
module Routing =

    open Microsoft.AspNetCore.Builder
    open Microsoft.OpenApi.Models
    open Giraffe
    open Giraffe.EndpointRouting

    let private getSchema (c: char) (modifier: string option) =
        match c with
        | 's' -> OpenApiSchema(Type = "string")
        | 'i' -> OpenApiSchema(Type = "integer", Format = "int32")
        | 'b' -> OpenApiSchema(Type = "boolean")
        | 'c' -> OpenApiSchema(Type = "string")
        | 'd' -> OpenApiSchema(Type = "integer", Format = "int64")
        | 'f' -> OpenApiSchema(Type = "number", Format = "double")
        | 'u' -> OpenApiSchema(Type = "integer", Format = "int64")
        | 'O' ->
            match modifier with
            | Some "guid" -> OpenApiSchema(Type = "string", Format = "uuid")
            | _ -> OpenApiSchema(Type = "string")
        | _ -> OpenApiSchema(Type = "string")
    
    let routef
        (path         : PrintfFormat<_,_,_,_, 'T>)
        (routeHandler : 'T -> HttpHandler) : Endpoint =
        let template, mappings = RouteTemplateBuilder.convertToRouteTemplate path
        let boxedHandler (o : obj) =
            let t = o :?> 'T
            routeHandler t
            
        let configureEndpoint =
            fun (endpoint: IEndpointConventionBuilder) ->
                endpoint.WithOpenApi(fun (operation: OpenApiOperation) ->
                    operation.Parameters <-
                        ResizeArray(
                            mappings
                            |> List.map(fun (name, format) ->
                                OpenApiParameter(
                                    Name = name,
                                    In = ParameterLocation.Path,
                                    Required = true,
                                    Style = ParameterStyle.Simple,
                                    Schema = getSchema format None
                                ))
                        )
                    operation)
                    
        TemplateEndpoint(HttpVerb.NotSpecified, template, mappings, boxedHandler, configureEndpoint)

    let addOpenApi (config: OpenApiConfig) = configureEndpoint config.Build

    let addOpenApiSimple<'Req, 'Res> =
        let methodName =
            match typeof<'Req>, typeof<'Res> with
            | reqType, respType when reqType = unitType && respType = unitType -> "InvokeUnit"
            | reqType, _ when reqType = unitType -> "InvokeUnitReq"
            | _, respType when respType = unitType -> "InvokeUnitResp"
            | _, _ -> "Invoke"
        configureEndpoint
            _.WithMetadata(
                typeof<FakeFunc<'Req, 'Res>>
                    .GetMethod(methodName, BindingFlags.Instance ||| BindingFlags.NonPublic)
            )
                .WithOpenApi()