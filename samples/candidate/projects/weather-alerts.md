# Weather Alerts

A background service that checks weather forecasts and sends alerts when rain or frost is expected.

## What it does

Every hour it fetches forecasts from a public weather API for a list of saved locations. When a forecast crosses a user's threshold, such as frost below 0°C, it sends an email.

## How it's built

- .NET Worker Service running on a schedule.
- Calls an external HTTP API with HttpClient, with retries using Polly when the API is slow or down.
- Stores locations and alert history in PostgreSQL.
- Logs with Serilog, and reports health checks for monitoring.

## What I learned

Handling an unreliable external service well, with timeouts, retries, and clear logs, made the difference between a toy and something I could leave running.
