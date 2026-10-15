<#
.SYNOPSIS
    Starts the semantic-engine proof of concept: infrastructure, models, backend services and the terminal client.

.DESCRIPTION
    1. Starts RabbitMQ, PostgreSQL, ClickHouse and Metabase with Docker Compose.
    2. Pulls the Ollama models that are not available locally.
    3. Builds the solution.
    4. Starts the Orchestrator, the Tool Worker, the Data Worker and the Gateway, each in its own console window.
    5. Starts the terminal client in the current console.

    Close the service windows to stop the backend. Use "docker compose down" to stop the infrastructure.

.PARAMETER SkipInfrastructure
    Do not run Docker Compose.

.PARAMETER SkipModels
    Do not check or pull the Ollama models.

.PARAMETER NoClient
    Start the backend only.

.EXAMPLE
    ./scripts/start-all.ps1
#>
[CmdletBinding()]
param(
    [switch]$SkipInfrastructure,
    [switch]$SkipModels,
    [switch]$NoClient
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

$chatModel = 'qwen2.5-coder:7b'
$embeddingModel = 'all-minilm:l6-v2'

if (-not $SkipInfrastructure) {
    Write-Host 'Starting infrastructure...' -ForegroundColor Cyan
    docker compose up -d
    if ($LASTEXITCODE -ne 0) { throw 'docker compose failed.' }
}

if (-not $SkipModels) {
    $installed = (ollama list) -join "`n"
    foreach ($model in @($chatModel, $embeddingModel)) {
        if ($installed -notmatch [regex]::Escape($model)) {
            Write-Host "Pulling $model..." -ForegroundColor Cyan
            ollama pull $model
            if ($LASTEXITCODE -ne 0) { throw "ollama pull $model failed." }
        }
    }
}

Write-Host 'Building...' -ForegroundColor Cyan
dotnet build Semantic.Engine.slnx --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$services = @(
    'src/Services/Semantic.Engine.Orchestrator',
    'src/Services/Semantic.Engine.Workers.Tools',
    'src/Services/Semantic.Engine.Workers.Data',
    'src/Api/Semantic.Engine.Gateway'
)

foreach ($service in $services) {
    $name = Split-Path $service -Leaf
    Write-Host "Starting $name..." -ForegroundColor Cyan
    Start-Process powershell -WorkingDirectory $root -ArgumentList @(
        '-NoExit', '-Command',
        "`$Host.UI.RawUI.WindowTitle = '$name'; dotnet run --no-build --project $service"
    )
}

if (-not $NoClient) {
    Write-Host 'Waiting for the services to start...' -ForegroundColor Cyan
    Start-Sleep -Seconds 15
    dotnet run --no-build --project src/Clients/Semantic.Engine.Client.Cli
}
