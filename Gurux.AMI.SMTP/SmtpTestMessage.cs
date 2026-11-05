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

namespace Gurux.AMI.SMTP
{
    /// <summary>
    /// Contains the recipient and optional temporary settings for an SMTP test email.
    /// </summary>
    public class SmtpTestMessage
    {
        /// <summary>
        /// Gets or sets the email address that receives the test message.
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Gets or sets temporary SMTP settings, or <see langword="null"/> to use the configured settings.
        /// </summary>
        /// <remarks>An empty temporary password reuses the configured password. Settings are not persisted.</remarks>
        public SmtpOptions? Options { get; set; }
    }
}
