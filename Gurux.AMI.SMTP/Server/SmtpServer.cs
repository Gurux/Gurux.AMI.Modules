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
using Gurux.DLMS.AMI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Mail;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Gurux.AMI.SMTP.Server;

/// <summary>Registers server-side SMTP services and handles test delivery requests.</summary>
[UnsupportedOSPlatform("browser")]
internal static class SmtpServer
{
    /// <summary>Registers the sender and policy-protected test endpoint on the server.</summary>
    internal static void Configure(AmiModuleBuilder builder)
    {
        builder.Services.AddScoped<GXSmtpSender>();
        builder.Services.AddScoped<IGXAmiEmailSender>(services => services.GetRequiredService<GXSmtpSender>());
        builder.Services.AddSingleton<ISmtpTransportFactory, SmtpTransportFactory>();
        builder.Export<IGXAmiEmailSender>();
        builder.MapPost("test", SendTestAsync).RequirePolicy("module.edit");
    }

    /// <summary>Validates a test email request and sends the module's localized test message.</summary>
    /// <param name="request">The HTTP request and module services used to process the test.</param>
    /// <param name="cancellationToken">A token that cancels request processing and email delivery.</param>
    /// <returns>A task that completes when the HTTP response has been written.</returns>
    /// <remarks>Returns HTTP 400 for invalid input, HTTP 502 for SMTP failures and HTTP 500 for unexpected failures.</remarks>
    private static async Task SendTestAsync(AmiModuleRequest request, CancellationToken cancellationToken)
    {
        SmtpTestMessage? message;
        try
        {
            message = await JsonSerializer.DeserializeAsync<SmtpTestMessage>(request.HttpContext.Request.Body,
                request.Module.JsonOptions, cancellationToken);
            if (message == null || string.IsNullOrWhiteSpace(message.Address))
            {
                throw new ArgumentException("Invalid receiver.");
            }

            _ = new MailAddress(message.Address);
            if (message.Options != null)
            {
                GXSmtpSender.ValidateOptions(message.Options);
            }
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
        {
            request.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            request.HttpContext.Response.ContentType = "application/problem+json";
            await request.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = StatusCodes.Status400BadRequest,
                title = "Invalid SMTP test request",
                detail = "The SMTP test request is invalid."
            }), cancellationToken);
            return;
        }
        try
        {
            await request.Module.Services.GetRequiredService<GXSmtpSender>().SendTestEmailAsync(message.Address!,
            Properties.Resources.GuruxDLMSAMISMTPTestEMail,
            Properties.Resources.IfYouReceiveThisMessageItMeansYourSiteIsCapableOfUsingSMTPToSendEMail,
            message.Options, cancellationToken);
            await request.HttpContext.Response.WriteAsync("ok", cancellationToken);
        }
        catch (SmtpException exception)
        {
            request.Module.HostServices.GetService<ILogger<GXSmtpModule>>()?.LogError(exception, "SMTP test email failed.");
            request.HttpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
            request.HttpContext.Response.ContentType = "application/problem+json";
            await request.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = StatusCodes.Status502BadGateway,
                title = "SMTP test email failed",
                detail = "The SMTP server could not deliver the test email."
            }), cancellationToken);
        }
        catch (TimeoutException exception)
        {
            request.Module.HostServices.GetService<ILogger<GXSmtpModule>>()?.LogWarning(exception, "SMTP test email timed out.");
            request.HttpContext.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            request.HttpContext.Response.ContentType = "application/problem+json";
            await request.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = StatusCodes.Status504GatewayTimeout,
                title = "SMTP test email timed out",
                detail = "The SMTP test email timed out."
            }), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            request.Module.HostServices.GetService<ILogger<GXSmtpModule>>()?.LogError(exception, "SMTP test email failed unexpectedly.");
            request.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            request.HttpContext.Response.ContentType = "application/problem+json";
            await request.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = StatusCodes.Status500InternalServerError,
                title = "SMTP test email failed",
                detail = "The SMTP test email could not be sent."
            }), cancellationToken);
        }
    }
}
