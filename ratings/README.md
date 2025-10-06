# 🎥 Ratings Backend Service with OpenTelemetry & EF Core

Aplikasi backend .NET 8 Minimal API untuk data rating, menggunakan SQLite sebagai database, serta observabilitas lengkap
menggunakan OpenTelemetry.

## 🚀 Fitur

- Endpoint RESTful untuk data rating.
- Tracing dan metrics dengan OpenTelemetry (gRPC & Prometheus).
- Integrasi Health Check untuk kebutuhan Kubernetes (readiness dan liveness).
- Middleware khusus untuk pencatatan metrik durasi request.
- Swagger UI untuk dokumentasi interaktif.

## ⚙️ Teknologi dan Library

- ASP.NET Core Minimal API
- `OpenTelemetry.Trace`
- `OpenTelemetry.Metrics`
- `OpenTelemetry.Exporter.Otlp`
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Prometheus Exporter`
- `HealthChecks` dari ASP.NET Core
- `Swashbuckle.AspNetCore` (Swagger)

## 🗂️ Struktur Proyek

```
├── Program.cs                  # Entry point & konfigurasi utama
├── AddHealthCheckHandler.cs    # Konfigurasi health check & middleware
├── AddObservabilityHandler.cs    # Konfigurasi observability & middleware
├── RatingDbContext.cs           # EF Core DbContext untuk movies.db
├── UpstreamAppInfoTracingMiddleware.cs        # Handler untuk tag span dengan informasi upstream app
├── Models/
│   ├── Rating.cs                # Entity Rating
├── appsettings.json            # Konfigurasi utama
├── appsettings.Development.json# Konfigurasi development
```

## 📊 Observabilitas OpenTelemetry

- Tracing: OTLP (gRPC) endpoint default `http://localhost:4317`
- Metrics:
    - `http_requests`: jumlah HTTP response per status & endpoint
    - `http_request_duration`: durasi permintaan HTTP (histogram)
    - `http_requests_in_progress`: gauge permintaan aktif
    - `up`: status aplikasi
- Prometheus scraping tersedia di `/metrics`

## 🔌 Endpoint API

| Method | Endpoint            | Deskripsi                   |
|--------|---------------------|-----------------------------|
| GET    | `/`                 | List semua ratings          |
| GET    | `/{movieId}` | Get ratings berdasarkan ID  |
| GET    | `/health/ready`     | Readiness health check      |
| GET    | `/health/live`      | Liveness health check       |
| GET    | `/metrics`          | Prometheus metrics scraping |
| GET    | `/swagger`          | UI dokumentasi Swagger      |

## 📦 Konfigurasi

Edit file `appsettings.json` atau `appsettings.{Environment}.json`:

```json
{
  "OpenTelemetry": {
    "Otlp": {
      "Endpoint": "http://localhost:4317"
    },
    "Exporters": {
      "Traces": "otlp",
      "Metrics": "prometheus"
    },
    "ResourceAttributes": {
      "ServiceName": "ratings",
      "ServiceVersion": "1.0.0"
    }
  }
}
```

## 🏁 Menjalankan Proyek

```bash
dotnet run
```

Lalu akses:  
•    http://localhost:5272/swagger  
•    http://localhost:5272/metrics  
•    http://localhost:5272/health/ready  
•    http://localhost:5272/health/live

![swagger](./swagger.png "swagger")
