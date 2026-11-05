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
