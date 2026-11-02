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

GNU General Public License v2.0 only (`GPL-2.0-only`).
