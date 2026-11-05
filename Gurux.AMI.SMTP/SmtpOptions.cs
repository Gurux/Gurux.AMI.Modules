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

using System.ComponentModel;

namespace Gurux.AMI.SMTP
{
    /// <summary>
    /// Defines the SMTP connection, sender and authentication settings used to send email.
    /// </summary>
    /// <remarks>The sender email address is also used as the SMTP authentication username.</remarks>
    public class SmtpOptions
    {
        /// <summary>
        /// Gets or sets the SMTP server hostname or IP address.
        /// </summary>
        [DefaultValue(null)]
        public string? Host { get; set; }

        /// <summary>
        /// Gets or sets the SMTP server port. The default is 587.
        /// </summary>
        [DefaultValue(587)]
        public int Port { get; set; } = 587;

        /// <summary>Gets or sets the maximum email delivery time in seconds. The default is 30.</summary>
        /// <remarks>Valid values are 1 through 300. Caller and module cancellation still take precedence.</remarks>
        [DefaultValue(30)]
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>Gets or sets whether to encrypt the connection using STARTTLS. The default is <see langword="true"/>.</summary>
        /// <remarks>This option does not enable implicit TLS connections.</remarks>
        [DefaultValue(true)]
        public bool EnableSsl { get; set; } = true;

        /// <summary>
        /// Gets or sets the sender email address, also used as the SMTP authentication username.
        /// </summary>
        [DefaultValue(null)]
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets the SMTP authentication password.
        /// </summary>
        /// <remarks>
        /// Persisted module settings contain an encrypted password. A password supplied for a test
        /// is plaintext; an empty test password reuses the configured password.
        /// </remarks>
        [DefaultValue(null)]
        public string? Password { get; set; }
    }
}


