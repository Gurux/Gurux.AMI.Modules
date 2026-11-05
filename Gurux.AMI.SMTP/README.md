# Gurux AMI SMTP

SMTP email module for Gurux DLMS AMI. Provides HTML email delivery through your SMTP server and a configuration view within AMI.

## Features

- SMTP server and sender account configuration.
- STARTTLS support.
- Test email delivery from the configuration view.
- Server-only SMTP services with safe error messages and detailed server-side logging.

## Configuration

Configure the module in Gurux DLMS AMI using your email provider's SMTP settings:

| Setting | Description |
|---|---|
| Server | SMTP server hostname or IP address |
| Port | SMTP port; default is `587` |
| Timeout | Maximum delivery time in seconds; default `30`, allowed range `1–300` |
| Email | Sender email address and SMTP username |
| Password | SMTP account password |
| STARTTLS | Encrypt the connection; enabled by default |

Use **Send test email** to verify the settings.

## License

This software is available under either of the following licenses:

- **Open-source license:** GNU General Public License, version 2 only
  (GPL-2.0-only). See [LICENSE](../LICENSE) for the full license text.
- **Commercial license:** Available from Gurux Ltd under a separate
  written agreement. This option allows use under the terms of that
  agreement instead of GPL-2.0-only.

A valid commercial license is required to use the commercial licensing
option. See [Gurux Commercial License Notice](../LICENSES/LicenseRef-Gurux-Commercial.txt).

For commercial licensing inquiries, [contact Gurux Ltd](https://www.gurux.fi/).
