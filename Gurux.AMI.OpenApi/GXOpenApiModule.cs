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
    public override string Id => "OpenApi";
    /// <inheritdoc />
    public override string Name => Properties.Resources.OpenAPI;
    /// <inheritdoc />
    public override string Description => Properties.Resources.GeneratesAPIDocumentationFromActiveAMIEndpoints;

    /// <inheritdoc />
    public override string Icon => "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"400\" height=\"400\" viewBox=\"0 0 400 400\" role=\"img\" aria-labelledby=\"title\">  <title id=\"title\">OpenAPI</title>  <path fill=\"#fff\" d=\"M0 0h400v400H0z\"/>  <g fill=\"#4d5930\">    <path d=\"M17 221a162 162 0 0 1 47-113l68 69a66 66 0 0 0-19 44Z\"/>    <path d=\"M185 60a162 162 0 0 1 86 28l-69 70a66 66 0 0 0-17-2Z\"/>    <path d=\"M312 130a162 162 0 0 1 28 85h-96a66 66 0 0 0-3-14Z\"/>    <path d=\"m224 274 64 68a162 162 0 0 1-20 14l-48-79Z\"/>    <path d=\"m147 283-36 86a162 162 0 0 1-41-28l68-63Z\"/>  </g>  <g fill=\"#6ba136\">    <path d=\"M95 83a162 162 0 0 1 84-23v96a66 66 0 0 0-33 10Z\"/>    <path d=\"M245 222h96a162 162 0 0 1-48 115l-67-67a66 66 0 0 0 19-48Z\"/>    <path d=\"m133 270-68 66a162 162 0 0 1-25-30l83-49a66 66 0 0 0 10 13Z\"/>  </g>  <g fill=\"#91d307\">    <path d=\"m70 102 20-15 51 82-4 3Z\"/>    <path d=\"M17 229h96a66 66 0 0 0 6 22l-82 49a162 162 0 0 1-20-71Z\"/>    <path d=\"m153 282-36 90a162 162 0 0 0 146-12l-49-81a66 66 0 0 1-61 3Z\"/>  </g>  <g fill=\"#3d3d3d\" stroke=\"#3d3d3d\">    <path d=\"m199 202 119-119\" fill=\"none\" stroke-width=\"29\"/>    <circle cx=\"179\" cy=\"222\" r=\"45\" stroke=\"none\"/>    <circle cx=\"338\" cy=\"63\" r=\"45\" stroke=\"none\"/>  </g></svg>";

    /// <inheritdoc />
    public override void Configure(AmiModuleBuilder builder)
    {
        builder.AddHostSingleton<EndpointDataSource>();
        builder.Services.AddSingleton<GXOpenApiDocumentProvider>();
        builder.Services.AddSingleton<GXOpenApiEndpointSource>();
        builder.AddEndpointSource<GXOpenApiEndpointSource>();
    }
}
