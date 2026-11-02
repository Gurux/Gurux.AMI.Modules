//
// --------------------------------------------------------------------------
//  Gurux Ltd
//
//
//
// Filename:        $HeadURL$
//
// Version:         $Revision$,
//                  $Date$
//                  $Author$
//
// Copyright (c) Gurux Ltd
//
//---------------------------------------------------------------------------
//
//  DESCRIPTION
//
// This file is a part of Gurux Device Framework.
//
// Gurux Device Framework is Open Source software; you can redistribute it
// and/or modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; version 2 of the License.
// Gurux Device Framework is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
//
// This code is licensed under the GNU General Public License v2.
// Full text may be retrieved at http://www.gnu.org/licenses/gpl-2.0.txt
//---------------------------------------------------------------------------
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace Gurux.AMI.OpenApi;

/// <summary>Publishes the documentation route only while the OpenApi module is active.</summary>
public sealed class GXOpenApiEndpointSource : EndpointDataSource
{
    private readonly IReadOnlyList<Endpoint> endpoints;

    /// <summary>Initializes a new instance of <see cref="GXOpenApiEndpointSource"/>.</summary>
    /// <param name="documents">The documents used by this operation.</param>
    public GXOpenApiEndpointSource(GXOpenApiDocumentProvider documents)
    {
        var builder = new RouteEndpointBuilder(async context =>
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(documents.GetDocument(), context.RequestAborted);
        }, RoutePatternFactory.Parse("/openapi/v1.json"), 0)
        {
            DisplayName = global::Gurux.AMI.OpenApi.Properties.Resources.OpenAPIV1Document
        };
        builder.Metadata.Add(new HttpMethodMetadata(["GET"]));
        builder.Metadata.Add(new ExcludeFromDescriptionAttribute());
        endpoints = [builder.Build()];
    }

    /// <inheritdoc />
    public override IReadOnlyList<Endpoint> Endpoints => endpoints;
    /// <inheritdoc />
    public override IChangeToken GetChangeToken() => new CancellationChangeToken(CancellationToken.None);
}
