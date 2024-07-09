namespace Giraffe.OpenApi

// Obtained from https://github.com/Lanayx/Oxpecker/blob/develop/src/Oxpecker.OpenApi/Configuration.fs.
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

open System
open System.Reflection
open System.Runtime.CompilerServices
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Metadata
open Microsoft.OpenApi.Models

[<AutoOpen>]
module Configuration =

    // This is a hack to prevent generating Func tag in open API
    [<CompilerGenerated>]
    type internal FakeFunc<'T, 'U> =
        member this.Invoke (_: 'T) = Unchecked.defaultof<'U>
        member this.InvokeUnitReq () = Unchecked.defaultof<'U>
        member this.InvokeUnitResp (_: 'T) = ()
        member this.InvokeUnit () = ()

    let internal fakeFuncMethod =
        typeof<FakeFunc<_, _>>
            .GetMethod("InvokeUnit", BindingFlags.Instance ||| BindingFlags.NonPublic)
    let internal unitType = typeof<unit>

    type RequestBody(?requestType: Type, ?contentTypes: string array, ?isOptional: bool) =
        let requestType = requestType |> Option.defaultValue null
        let contentTypes = contentTypes |> Option.defaultValue [| "application/json" |]
        let isOptional = isOptional |> Option.defaultValue false
        member this.ToAttribute () = AcceptsMetadata(contentTypes, requestType, isOptional)

    type ResponseBody(?responseType: Type, ?contentTypes: string array, ?statusCode: int) =
        let responseType = responseType |> Option.defaultValue null
        let contentTypes = contentTypes |> Option.defaultValue null
        let statusCode = statusCode |> Option.defaultValue 200
        member this.ToAttribute () =
            ProducesResponseTypeMetadata(statusCode, responseType, contentTypes)

    type OpenApiConfig
        (
            ?requestBody: RequestBody,
            ?responseBodies: ResponseBody seq,
            ?configureOperation: OpenApiOperation -> OpenApiOperation
        )
        =

        member this.Build (builder: IEndpointConventionBuilder) =
            builder.WithMetadata(fakeFuncMethod) |> ignore
            requestBody
            |> Option.iter (fun accepts -> builder.WithMetadata(accepts.ToAttribute()) |> ignore)
            responseBodies
            |> Option.iter (fun responseInfos ->
                for produces in responseInfos do
                    builder.WithMetadata(produces.ToAttribute()) |> ignore
            )
            match configureOperation with
            | Some configure -> builder.WithOpenApi(configure)
            | None -> builder.WithOpenApi()
