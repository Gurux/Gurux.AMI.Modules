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

using Gurux.UI.Components;
using Gurux.UI.Components.Enums;
using Gurux.UI.Components.Help;

namespace Gurux.AMI.SMTP.Help;

/// <summary>Provides embedded user instructions for the SMTP module and its available screens.</summary>
public sealed class GXSmtpHelpProvider : IGXHelpProvider
{
    private static readonly IReadOnlyDictionary<string, string> Topics = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["module:Gurux.AMI.SMTP"] = "Overview.md",
        ["module:Gurux.AMI.SMTP:configuration"] = "Configuration.md"
    };

    /// <summary>Loads the module-owned Markdown topic, falling back to the embedded English instructions.</summary>
    /// <param name="request">Exact module, configuration or view topic to load.</param>
    /// <param name="cancellationToken">Token used to cancel documentation loading.</param>
    /// <returns>The requested guide, or null when this module does not own the topic.</returns>
    public async Task<GXHelpContent?> GetHelpAsync(GXHelpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Topics.TryGetValue(request.Topic, out var file))
        {
            return null;
        }
        await using var stream = typeof(GXSmtpHelpProvider).Assembly.GetManifestResourceStream("Gurux.AMI.SMTP.Help." + file)
            ?? throw new InvalidOperationException("Embedded module help is missing: " + file);
        using var reader = new StreamReader(stream);
        return new GXHelpContent(HelpKind.Markdown, await reader.ReadToEndAsync(cancellationToken));
    }
}
