# FeedBack.API (ASP.NET Core)

Backend API for the Feedback platform.

## Features

- JWT authentication (`/api/auth/login`, `/api/auth/register`)
- Public feedback submission (`POST /api/feedback/submit`)
- Dashboard analytics (JWT protected)
- Personnel management (JWT protected)
- Background email processing via Hangfire
- Rate limiting with Redis
- Observability: Serilog logs, Prometheus metrics, OpenTelemetry, Application Insights
- Health checks: `/health`

## Requirements

- .NET 8 SDK
- SQL Server
- Redis (for rate limiting)
- SendGrid (for email notifications)

## Local Development

### 1) Configure settings
Edit:
- `FeedBack.API/FeedBack.API/appsettings.json`

Key settings:
- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:HangfireConnection`
- `ConnectionStrings:Redis`
- `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`
- `SendGrid:ApiKey`

> The app seeds a default admin user on startup (if the `Users` table is empty):
> - Email: `admin@xdsdata.com`
> - Password: `Admin123!`

### 2) Run the API
From `FeedBack.API/FeedBack.API`:

```bash
dotnet restore
dotnet run
```

Swagger (development):
- `/swagger`
- `/swagger/index.html`

Health check:
- `GET /health`

Prometheus metrics:
- `GET /metrics`
- `GET /scrape` (scraping endpoint depends on instrumentation)

## API Endpoints

Base path is: `api/[controller]`

### Authentication (public)
- `POST /api/auth/login`
- `POST /api/auth/register`

Request bodies are defined by DTOs in `Dtos/`.

### Feedback (public)
- `POST /api/feedback/submit`

Body: `FeedbackRequestDto`
- `rating` (required, 0-10)
- `source` (optional, defaults to `website`; `premises` / `product_service` / `website`)
- `comment` (optional, max 200)
- `customerName` (optional)
- `customerEmail` (optional, email)
- `institution` (optional)
- `category` (optional)
- `personnelName` (optional)

Response: `FeedbackResponseDto`
- `success`, `ticketNumber`, `message`, `createdAt`

### Dashboard (JWT protected)
All endpoints require a Bearer token.

- `GET /api/dashboard/stats`
  - DashboardStatsDto: totals, averages, NPS, trend, distribution, personnel stats

- `GET /api/dashboard/feedbacks?source=&personnel=&search=&limit=`
  - Returns a list of feedback items with filtering and pagination (default `limit=200`).

- `GET /api/dashboard/personnel`
  - Returns distinct personnel names.

### Personnel management (JWT protected)
- `GET /api/personnel`
  - Returns all personnel

- `POST /api/personnel`
  - Adds a personnel entry

- `DELETE /api/personnel/{id}`
  - Deletes personnel by id

## Background Jobs / Email Queue

- Hangfire server enabled during startup.
- Recurring job: `process-email-queue` runs every minute (`*/1 * * * *`).
- Email provider: SendGrid (`SendGrid` section in configuration).

## Monitoring

- **Logging**: Serilog writes to console and rolling files in `logs/`.
- **Metrics**: Prometheus exporter + `prometheus-net` endpoints.
- **Custom endpoint**:
  - `GET /metrics/custom`

## Docker

A `Dockerfile` and `docker-compose.yml` exist under the `FeedBack.API/` folder.

## References

- Controllers:
  - `Controllers/AuthController.cs`
  - `Controllers/FeedbackController.cs`
  - `Controllers/DashboardController.cs`
  - `Controllers/PersonnelController.cs`

- Swagger is enabled in Development.

## License

Internal project documentation.

