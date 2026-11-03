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
using Gurux.DLMS.AMI.Module.Enums;
using Gurux.DLMS.AMI.Shared.DTOs.Module;
using Microsoft.Extensions.DependencyInjection;

namespace Gurux.AMI.Scalar;

/// <summary>Provides Scalar using the document owned by the OpenApi module.</summary>
public sealed class GXScalarModule : GXAmiModuleBase
{
    /// <inheritdoc />
    public override string Id => "Gurux.AMI.Scalar";
    /// <inheritdoc />
    public override string Name => global::Gurux.AMI.Scalar.Properties.Resources.Scalar;
    /// <inheritdoc />
    public override string Description => global::Gurux.AMI.Scalar.Properties.Resources.InteractiveScalarAPIDocumentation;
    /// <inheritdoc />
    public override IReadOnlyList<AmiModuleDependency> Dependencies => [new(AmiModuleDependencyKind.Module, "Gurux.AMI.OpenApi")];

    /// <inheritdoc />
    public override string Icon => Properties.Resources.ScalarIcon;

    /// <inheritdoc />
    public override void Configure(AmiModuleBuilder builder)
    {
        builder.Services.AddSingleton<GXScalarEndpointSource>();
        builder.AddEndpointSource<GXScalarEndpointSource>();
    }

    /// <inheritdoc />
    public override Task StartAsync(AmiModuleContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.Services.GetRequiredService<GXScalarEndpointSource>().Initialize(context.HostServices);
        return Task.CompletedTask;
    }
}

