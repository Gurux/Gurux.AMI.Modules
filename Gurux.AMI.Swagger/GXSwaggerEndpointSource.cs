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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace Gurux.AMI.Swagger;

/// <summary>Owns Swagger UI and its embedded assets without registering host middleware.</summary>
public sealed class GXSwaggerEndpointSource : EndpointDataSource
{
    private IReadOnlyList<Endpoint> endpoints = [];

    /// <summary>Initializes the swagger endpoint source data.</summary>
    /// <param name="hostServices">The host services used by this operation.</param>
    public void Initialize(IServiceProvider hostServices)
    {
        var application = new ApplicationBuilder(hostServices);
        application.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint("../openapi/v1.json", "Gurux.DLMS.AMI API v1");
            options.ConfigObject.AdditionalItems["tagsSorter"] = "alpha";
            options.ConfigObject.AdditionalItems["operationsSorter"] = "alpha";
        });
        var endpoint = new RouteEndpointBuilder(application.Build(), RoutePatternFactory.Parse("/swagger/{**asset}"), 0)
        {
            DisplayName = global::Gurux.AMI.Swagger.Properties.Resources.Swagger
        };
        endpoint.Metadata.Add(new HttpMethodMetadata(["GET", "HEAD"]));
        endpoint.Metadata.Add(new ExcludeFromDescriptionAttribute());
        endpoints = [endpoint.Build()];
    }

    /// <inheritdoc />
    public override IReadOnlyList<Endpoint> Endpoints => endpoints;
    /// <inheritdoc />
    public override IChangeToken GetChangeToken() => new CancellationChangeToken(CancellationToken.None);
}
