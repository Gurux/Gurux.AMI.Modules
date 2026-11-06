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
using Gurux.DLMS.AMI.Module.Enums;
using Gurux.DLMS.AMI.Shared.DTOs.Module;

namespace Gurux.AMI.Swagger;

/// <summary>Provides Swagger UI using the document owned by the OpenApi module.</summary>
public sealed class GXSwaggerModule : GXAmiModuleBase
{
    /// <inheritdoc />
    public override string Id => "Gurux.AMI.Swagger";

    /// <inheritdoc />
    public override string Name => Properties.Resources.Swagger;

    /// <inheritdoc />
    public override string Description => Properties.Resources.InteractiveSwaggerAPIDocumentation;
    /// <inheritdoc />
    public override IReadOnlyList<AmiModuleDependency> Dependencies => [new(AmiModuleDependencyKind.Module, "Gurux.AMI.OpenApi")];

    /// <inheritdoc />
    public override string Icon => Properties.Resources.SwaggerIcon;

    /// <inheritdoc />
    public override void Configure(AmiModuleBuilder builder)
    {
        builder.AddHelp<global::Gurux.AMI.Swagger.Help.GXSwaggerHelpProvider, GXHelpContent>("module.view",
            static (provider, request, token) => provider.GetHelpAsync(
                new GXHelpRequest(request.Topic, request.Version, request.Culture), token));
        builder.Services.AddSingleton<IGXHelpProvider>(services =>
            services.GetRequiredService<global::Gurux.AMI.Swagger.Help.GXSwaggerHelpProvider>());
        builder.Services.AddSingleton<GXSwaggerEndpointSource>();
        builder.AddEndpointSource<GXSwaggerEndpointSource>();
    }

    /// <inheritdoc />
    public override Task StartAsync(AmiModuleContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.Services.GetRequiredService<GXSwaggerEndpointSource>().Initialize(context.HostServices);
        return Task.CompletedTask;
    }
}

