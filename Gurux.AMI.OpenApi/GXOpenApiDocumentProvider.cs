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
using Gurux.AMI.OpenApi.Internal;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Gurux.AMI.OpenApi;

/// <summary>Caches serialized documentation until the host endpoint catalog changes.</summary>
public sealed class GXOpenApiDocumentProvider(EndpointDataSource source)
{
    private readonly Lock gate = new();
    private string? document;
    private IChangeToken? changes;

    /// <summary>Gets document.</summary>
    /// <returns>The result of the operation.</returns>
    public string GetDocument()
    {
        lock (gate)
        {
            if (document == null || changes == null || changes.HasChanged)
            {
                // Acquire the token first so a concurrent catalog change invalidates this snapshot.
                changes = source.GetChangeToken();
                document = GXOpenApiSchemaFilter.GenerateDocument(source.Endpoints);
            }
            return document;
        }
    }
}
