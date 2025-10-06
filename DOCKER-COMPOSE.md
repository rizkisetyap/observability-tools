
# 🐳 Docker Compose untuk Sistem Observabilitas dan Pengujian Beban

## 📋 Deskripsi Umum

File `docker-compose.yml` ini mengatur sebuah sistem yang terdiri dari beberapa layanan mikroservice dan komponen observabilitas yang berjalan di dalam Docker container. Sistem ini mencakup:

- 🖥️ **Backend, Movies, Ratings**: Tiga aplikasi mikroservice yang saling berinteraksi dan menyediakan metrik untuk monitoring.
- 📊 **Prometheus**: Sistem monitoring yang mengumpulkan metrik dari layanan.
- 🔔 **Alertmanager**: Menangani pemberitahuan (alert) dari Prometheus.
- 📈 **Grafana**: Visualisasi metrik dan trace data.
- 🔍 **Tempo**: Tracing distributed untuk observasi aplikasi.
- 🚦 **k6**: Alat uji beban untuk menguji performa layanan.

Semua layanan berkomunikasi melalui jaringan Docker `app-net` yang sama untuk kemudahan integrasi dan komunikasi internal.

---

## 🛠️ Rincian Layanan

### 1. Backend

- **Deskripsi**: Aplikasi backend utama yang menyediakan endpoint dan metrik yang akan dipantau.
- **Build**: Menggunakan Dockerfile di `./backend`.
- **Port**: Terexpose di host `5001` mengarah ke `8080` dalam container.
- **Environment Variables**:
    - `MoviesApi__BaseUrl=http://movies:8080`
    - `RatingsApi__BaseUrl=http://ratings:8080`
    - `OpenTelemetry__Otlp__Endpoint=http://tempo:4317`
    - `OpenTelemetry__Exporters__Traces=otlp`
    - `OpenTelemetry__Exporters__Metrics=prometheus`
    - `OpenTelemetry__Exporters__Logs=none`
- **Dependencies**: Bergantung pada layanan `movies` dan `ratings`.
- **Networks**: app-net

```yaml
backend:
  build: ./backend
  container_name: backend
  ports:
    - "5001:8080"
  environment:
    - MoviesApi__BaseUrl=http://movies:8080
    - RatingsApi__BaseUrl=http://ratings:8080
    - OpenTelemetry__Otlp__Endpoint=http://tempo:4317
    - OpenTelemetry__Exporters__Traces=otlp
    - OpenTelemetry__Exporters__Metrics=prometheus
    - OpenTelemetry__Exporters__Logs=none
  networks:
    - app-net
  depends_on:
    - movies
    - ratings
```

---

### 2. Movies

- **Deskripsi**: Service penyedia data movie, yang juga menggunakan OpenTelemetry dan terhubung ke `ratings`.
- **Build**: Dari `./movies`.
- **Port**: Host `5002` ke container `8080`.
- **Environment Variables**:
    - `RatingsApi__BaseUrl=http://ratings:8080`
    - OpenTelemetry settings serupa backend.
- **Dependencies**: Bergantung pada `ratings`.
- **Networks**: app-net

```yaml
movies:
  build: ./movies
  container_name: movies
  ports:
    - "5002:8080"
  environment:
    - RatingsApi__BaseUrl=http://ratings:8080
    - OpenTelemetry__Otlp__Endpoint=http://tempo:4317
    - OpenTelemetry__Exporters__Traces=otlp
    - OpenTelemetry__Exporters__Metrics=prometheus
    - OpenTelemetry__Exporters__Logs=none
  networks:
    - app-net
  depends_on:
    - ratings
```

---

### 3. Ratings

- **Deskripsi**: Service penyedia data rating, termasuk OpenTelemetry untuk trace dan metric.
- **Build**: Dari `./ratings`.
- **Port**: Host `5003` ke container `8080`.
- **Environment Variables**: OpenTelemetry konfigurasi.
- **Networks**: app-net

```yaml
ratings:
  build: ./ratings
  container_name: ratings
  ports:
    - "5003:8080"
  environment:
    - OpenTelemetry__Otlp__Endpoint=http://tempo:4317
    - OpenTelemetry__Exporters__Traces=otlp
    - OpenTelemetry__Exporters__Metrics=prometheus
    - OpenTelemetry__Exporters__Logs=none
  networks:
    - app-net
```

---

### 4. Prometheus

- **Deskripsi**: Server monitoring Prometheus, mengumpulkan metrik dari layanan.
- **Image**: `prom/prometheus`
- **Port**: Host `9090`
- **Volumes**:
    - Konfigurasi Prometheus (`prometheus.yml`, alert rules, recording rules) dari folder lokal `./prometheus`.
    - Volume data `prometheus-data` untuk persistensi.
- **Dependencies**: Bergantung pada `otel-collector` (komentar, bisa diaktifkan sesuai kebutuhan).
- **Networks**: app-net

```yaml
prometheus:
  image: prom/prometheus
  container_name: prometheus
  volumes:
    - ./prometheus/prometheus.yml:/etc/prometheus/prometheus.yml
    - ./prometheus/alert.rules.yml:/etc/prometheus/alert.rules.yml
    - ./prometheus/recording.rules.yml:/etc/prometheus/recording.rules.yml
    - prometheus-data:/prometheus
  ports:
    - "9090:9090"
  depends_on:
    - otel-collector
  networks:
    - app-net
```

---

### 5. Tempo

- **Deskripsi**: Tracing distributed untuk observabilitas aplikasi.
- **Image**: `grafana/tempo:latest`
- **Port**: Host 3200 (HTPP), 4317 dan 4318 (OTLP)
- **Networks**: app-net

```yaml
  init-tempo:
    image: grafana/tempo:latest
    user: root
    entrypoint:
      - "chown"
      - "10001:10001"
      - "/var/tempo"
    volumes:
      - ./tempo/data:/var/tempo
  tempo:
    image: grafana/tempo:latest
    container_name: tempo
    ports:
      - "3200:3200" # HTTP query
      - "4317:4317" # OTLP gRPC
      - "4318:4318" # OTLP HTTP
    command: -config.file=/etc/tempo/config.yaml
    volumes:
      - ./tempo/config/config.yaml:/etc/tempo/config.yaml
      - ./tempo/data:/var/tempo # create directory for data
    networks:
      - app-net
    depends_on:
      - init-tempo
```

---

### 6. Alertmanager

- **Deskripsi**: Menerima alert dari Prometheus, mengelola notifikasi.
- **Image**: `prom/alertmanager`
- **Port**: Host 9093
- **Volume**: Konfigurasi dari `./alert-manager/alert-manager.yml`
- **Dependencies**: `prometheus`
- **Networks**: app-net

```yaml
alert-manager:
  image: prom/alertmanager
  container_name: alert-manager
  ports:
    - "9093:9093"
  volumes:
    - ./alert-manager/alert-manager.yml:/etc/alertmanager/alertmanager.yml
  networks:
    - app-net
  depends_on:
    - prometheus
```

---

### 7. Grafana

- **Deskripsi**: Visualisasi metrik dan trace dari Prometheus dan Tempo.
- **Image**: `grafana/grafana`
- **Port**: Host 3000
- **Environment**:
    - Anonimous access enabled (Admin role)
    - Login form disabled (sesuaikan kebutuhan keamanan)
    - Fitur `traceqlEditor` diaktifkan
- **Volumes**:
    - Dashboards dan provisioning dari folder lokal
    - Data persisten di volume `grafana-data`
- **Dependencies**: `prometheus` dan `tempo`
- **Networks**: app-net

```yaml
grafana:
  image: grafana/grafana
  container_name: grafana
  environment:
    - GF_AUTH_ANONYMOUS_ENABLED=true
    - GF_AUTH_ANONYMOUS_ORG_ROLE=Admin
    - GF_AUTH_DISABLE_LOGIN_FORM=true
    - GF_FEATURE_TOGGLES_ENABLE=traceqlEditor
  volumes:
    - ./grafana/dashboards:/var/lib/grafana/dashboards
    - ./grafana/provisioning/dashboards:/etc/grafana/provisioning/dashboards
    - ./grafana/provisioning/datasources:/etc/grafana/provisioning/datasources
    - grafana-data:/var/lib/grafana
  ports:
    - "3000:3000"
  networks:
    - app-net
  depends_on:
    - prometheus
    - tempo
```

---

### 8. k6

- **Deskripsi**: Alat uji beban yang menjalankan skrip dari `./k6/script.js`.
- **Image**: `grafana/k6:latest`
- **Volumes**:
    - Skrip uji beban
    - Folder `./k6/results` untuk hasil
- **Command**: Menjalankan `run /script.js`
- **Environment Variables**:
    - URL layanan backend, movies, ratings dengan alamat internal container
- **Dependencies**: `backend`, `movies`, `ratings`, `prometheus`, dan `tempo`
- **Networks**: app-net

```yaml
k6:
  image: grafana/k6:latest
  container_name: k6
  volumes:
    - ./k6/script.js:/script.js
    - ./k6/results:/results
  command: run /script.js
  environment:
    - BACKEND_URL=http://backend:8080
    - MOVIE_URL=http://movies:8080
    - RATING_URL=http://ratings:8080
  networks:
    - app-net
  depends_on:
    - backend
    - movies
    - ratings
    - prometheus
    - tempo
```

---

### 9. Jaringan dan Volume

- Semua layanan menggunakan jaringan Docker bernama `app-net`.
- Volume persistensi:
    - `prometheus-data` untuk Prometheus
    - `grafana-data` untuk Grafana

```yaml
networks:
  app-net:

volumes:
  prometheus-data:
  grafana-data:
```

---

## Cara Penggunaan

1. **Persiapan**

   Pastikan Docker dan Docker Compose sudah terinstal pada sistem Anda.

2. **Jalankan Semua Layanan**

   Jalankan perintah berikut di direktori yang berisi `docker-compose.yml`:

   ```bash
   docker-compose up -d
   ```

   Perintah ini akan menjalankan semua container di background.

3. **Akses Layanan**

    - **Grafana**: http://localhost:3000  
      (User default: admin, password default bisa disesuaikan. Dalam konfigurasi ini login form dimatikan, akses anonim dengan peran admin diaktifkan.)

    - **Prometheus**: http://localhost:9090

    - **Alertmanager**: http://localhost:9093

    - **Backend**: http://localhost:5001

    - **Movies**: http://localhost:5002

    - **Ratings**: http://localhost:5003

4. **Uji Beban dengan k6**

   k6 otomatis menjalankan skrip uji beban yang ada di `./k6/script.js` dan menyimpan hasil di folder `./k6/results`. Laporan uji beban bisa dilihat di terminal tempat Docker Compose dijalankan.

---

## Catatan Tambahan

- Jika ingin menggunakan OpenTelemetry Collector (`otel-collector`) dan Loki sebagai sistem logging, Anda dapat mengaktifkan konfigurasi yang sudah dikomentari di `docker-compose.yml`.

- Pastikan file konfigurasi dan direktori volume (`./prometheus`, `./alert-manager`, `./grafana`, `./k6`, dll) sudah tersedia dan berisi konfigurasi yang benar.

- Sesuaikan environment variables dan konfigurasi layanan sesuai kebutuhan lingkungan dan keamanan Anda.

---
