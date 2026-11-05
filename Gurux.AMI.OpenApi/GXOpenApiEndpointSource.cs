// --------------------------------------------------------------------------
// Gurux Ltd
//
// Copyright (c) Gurux Ltd.
//
// This file is part of Gurux Device Framework.
//
// Commit: $Format:%H$
// Date:   $Format:%cI$
//
// This software is available under either of the following licenses:
//
// 1. GNU General Public License, version 2 only (GPL-2.0-only).
//    See the LICENSE file included with this distribution.
//
// 2. A commercial license granted by Gurux Ltd under a separate written
//    agreement. Use under this option requires a valid commercial license
//    and is governed by the terms of that agreement.
//
// If you have not obtained a commercial license from Gurux Ltd,
// use of this software is governed by GPL-2.0-only.
//
// Commercial licensing: https://www.gurux.fi/
//
// SPDX-License-Identifier: GPL-2.0-only OR LicenseRef-Gurux-Commercial
// --------------------------------------------------------------------------

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
