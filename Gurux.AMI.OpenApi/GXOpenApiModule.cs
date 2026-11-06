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

using Gurux.DLMS.AMI.Module;
using Gurux.UI.Components;
using Gurux.UI.Components.Help;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;

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
        builder.AddHelp<global::Gurux.AMI.OpenApi.Help.GXOpenApiHelpProvider, GXHelpContent>("module.view",
            static (provider, request, token) => provider.GetHelpAsync(
                new GXHelpRequest(request.Topic, request.Version, request.Culture), token));
        builder.Services.AddSingleton<IGXHelpProvider>(services =>
            services.GetRequiredService<global::Gurux.AMI.OpenApi.Help.GXOpenApiHelpProvider>());
        builder.AddHostSingleton<EndpointDataSource>();
        builder.Services.AddSingleton<GXOpenApiDocumentProvider>();
        builder.Services.AddSingleton<GXOpenApiEndpointSource>();
        builder.AddEndpointSource<GXOpenApiEndpointSource>();
    }
}

