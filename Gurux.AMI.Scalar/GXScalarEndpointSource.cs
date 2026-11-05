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
using Microsoft.Extensions.Primitives;
using Scalar.AspNetCore;

namespace Gurux.AMI.Scalar;

/// <summary>Builds module-owned Scalar endpoints using only ASP.NET Core host contracts.</summary>
public sealed class GXScalarEndpointSource : EndpointDataSource
{
    private IReadOnlyList<Endpoint> endpoints = [];

    /// <summary>Initializes the scalar endpoint source data.</summary>
    /// <param name="hostServices">The host services used by this operation.</param>
    public void Initialize(IServiceProvider hostServices)
    {
        var routes = new Routes(hostServices);
        routes.MapScalarApiReference(options => options.WithOpenApiRoutePattern("/openapi/{documentName}.json"));
        endpoints = routes.DataSources.SelectMany(source => source.Endpoints).ToArray();
    }

    /// <inheritdoc />
    public override IReadOnlyList<Endpoint> Endpoints => endpoints;
    /// <inheritdoc />
    public override IChangeToken GetChangeToken() => new CancellationChangeToken(CancellationToken.None);

    private sealed class Routes(IServiceProvider services) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider => services;
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(services);
    }
}
