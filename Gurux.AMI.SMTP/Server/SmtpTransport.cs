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

using System.Net;
using System.Net.Mail;
using System.Runtime.Versioning;

namespace Gurux.AMI.SMTP.Server;

/// <summary>Provides an SMTP transport for a single email operation.</summary>
/// <remarks>The caller owns the transport and must dispose it after sending.</remarks>
[UnsupportedOSPlatform("browser")]
public interface ISmtpTransport : IDisposable
{
    /// <summary>Sends the supplied email message asynchronously.</summary>
    /// <param name="message">The message to send. The caller retains ownership of the message.</param>
    /// <param name="cancellationToken">A token that cancels the send operation.</param>
    /// <returns>A task that completes when the send operation finishes.</returns>
    Task SendAsync(MailMessage message, CancellationToken cancellationToken);
}

/// <summary>Creates SMTP transports with independent lifetimes.</summary>
[UnsupportedOSPlatform("browser")]
public interface ISmtpTransportFactory
{
    /// <summary>Creates a transport configured with the supplied SMTP settings.</summary>
    /// <param name="options">The validated connection settings, including any plaintext authentication password.</param>
    /// <returns>A transport that the caller must dispose after use.</returns>
    ISmtpTransport Create(SmtpOptions options);
}

/// <summary>Creates SMTP transports backed by <see cref="SmtpClient"/>.</summary>
/// <remarks>
/// Uses the sender email address as the authentication username when a password is supplied.
/// When enabled, connection encryption uses STARTTLS rather than implicit TLS.
/// </remarks>
[UnsupportedOSPlatform("browser")]
public sealed class SmtpTransportFactory : ISmtpTransportFactory
{
    /// <inheritdoc />
    public ISmtpTransport Create(SmtpOptions options) => new Transport(options);

    /// <summary>Wraps the SMTP client used by one email operation.</summary>
    private sealed class Transport : ISmtpTransport
    {
        /// <summary>The SMTP client owned by this transport.</summary>
        private readonly SmtpClient client;
        /// <summary>Initializes the SMTP client without opening a connection.</summary>
        /// <param name="options">The connection and authentication settings.</param>
        public Transport(SmtpOptions options)
        {
            client = new SmtpClient(options.Host!, options.Port) { UseDefaultCredentials = false, EnableSsl = options.EnableSsl };
            if (!string.IsNullOrEmpty(options.Password))
            {
                client.Credentials = new NetworkCredential(options.Email, options.Password);
            }
        }
        /// <inheritdoc />
        public Task SendAsync(MailMessage message, CancellationToken cancellationToken)
            => client.SendMailAsync(message, cancellationToken);
        /// <summary>Releases the SMTP client and its connection resources.</summary>
        public void Dispose() => client.Dispose();
    }
}

