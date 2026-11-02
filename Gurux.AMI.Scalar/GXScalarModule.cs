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
    public override string Id => "Scalar";
    /// <inheritdoc />
    public override string Name => global::Gurux.AMI.Scalar.Properties.Resources.Scalar;
    /// <inheritdoc />
    public override string Description => global::Gurux.AMI.Scalar.Properties.Resources.InteractiveScalarAPIDocumentation;
    /// <inheritdoc />
    public override IReadOnlyList<AmiModuleDependency> Dependencies => [new(AmiModuleDependencyKind.Module, "OpenApi")];

    /// <inheritdoc />
    public override string Icon => "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"554\" height=\"554\" viewBox=\"0 0 554 554\">  <rect width=\"554\" height=\"554\" fill=\"#fff\"/>  <g fill=\"#1b1b1a\">    <path d=\"M219 11Q219 0 230 0H324Q335 0 335 11V136L423 48Q431 40 439 48L506 115Q513 123 506 131L418 219H543Q554 219 554 230V324Q554 335 543 335H418L506 423Q513 431 506 439L439 506Q431 513 423 506L335 418V543Q335 554 324 554H230Q219 554 219 543V478Q219 446 242 423L362 303Q383 282 362 254L242 133Q219 110 219 78Z\"/>    <path d=\"M114 49Q122 41 130 49L333 252Q355 277 333 302L130 506Q122 513 114 506L48 440Q41 432 48 424L136 335H11Q0 335 0 324V230Q0 219 11 219H136L49 131Q41 123 49 115Z\"/>  </g></svg>";

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
