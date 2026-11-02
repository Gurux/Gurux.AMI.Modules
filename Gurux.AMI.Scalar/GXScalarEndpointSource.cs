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
