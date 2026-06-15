# setup-monitoring.ps1
Write-Host "Setting up Prometheus monitoring for Feedback API" -ForegroundColor Green

# Create directories
$basePath = $PSScriptRoot
$prometheusPath = Join-Path $basePath "prometheus"
$grafanaProvisioningPath = Join-Path $basePath "grafana\provisioning"
$grafanaDatasourcesPath = Join-Path $grafanaProvisioningPath "datasources"
$grafanaDashboardsPath = Join-Path $grafanaProvisioningPath "dashboards"

# Create directories if they don't exist
$dirs = @($prometheusPath, $grafanaDatasourcesPath, $grafanaDashboardsPath)
foreach ($dir in $dirs) {
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "Created directory: $dir" -ForegroundColor Yellow
    }
}

# Create prometheus.yml if it doesn't exist
$prometheusConfigPath = Join-Path $basePath "prometheus.yml"
$prometheusConfigDest = Join-Path $prometheusPath "prometheus.yml"

if (Test-Path $prometheusConfigPath) {
    if (Test-Path $prometheusConfigDest) {
        Write-Host "prometheus.yml already exists, skipping..." -ForegroundColor Yellow
    } else {
        Copy-Item $prometheusConfigPath $prometheusConfigDest -Force
        Write-Host "Copied prometheus.yml" -ForegroundColor Green
    }
} else {
    Write-Host "prometheus.yml not found, creating default..." -ForegroundColor Yellow
    @"
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'feedback-api'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['localhost:5175']
"@ | Out-File -FilePath $prometheusConfigDest -Encoding UTF8
}

# Create grafana datasource config
$grafanaDatasourcePath = Join-Path $grafanaDatasourcesPath "prometheus.yml"
if (!(Test-Path $grafanaDatasourcePath)) {
    @"
apiVersion: 1

datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    editable: true
"@ | Out-File -FilePath $grafanaDatasourcePath -Encoding UTF8
    Write-Host "Created Grafana datasource config" -ForegroundColor Green
}

Write-Host "Setup complete!" -ForegroundColor Green
Write-Host "To start monitoring, run: docker-compose -f docker-compose.monitoring.yml up -d" -ForegroundColor Cyan