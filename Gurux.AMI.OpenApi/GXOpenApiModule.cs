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
using Gurux.DLMS.AMI.Module;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Gurux.AMI.OpenApi;

/// <summary>Provides OpenAPI documents for the host's currently registered HTTP endpoints.</summary>
public sealed class GXOpenApiModule : GXAmiModuleBase
{
    /// <inheritdoc />
    public override string Id => "Gurux.AMI.OpenApi";
    /// <inheritdoc />
    public override string Name => Properties.Resources.OpenAPI;
    /// <inheritdoc />
    public override string Description => Properties.Resources.GeneratesAPIDocumentationFromActiveAMIEndpoints;

    /// <inheritdoc />
    public override string Icon => Properties.Resources.OpenApiIcon;
    /// <inheritdoc />
    public override void Configure(AmiModuleBuilder builder)
    {
        builder.AddHostSingleton<EndpointDataSource>();
        builder.Services.AddSingleton<GXOpenApiDocumentProvider>();
        builder.Services.AddSingleton<GXOpenApiEndpointSource>();
        builder.AddEndpointSource<GXOpenApiEndpointSource>();
    }
}

