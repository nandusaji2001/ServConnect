param(
    [string]$BaseUrl = "https://localhost:7213",
    [string]$Browser = "chrome",
    [string]$Email = "user@example.com",
    [string]$Password = "Test@123",
    [switch]$Headed
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$backendPath = Join-Path $projectRoot "backend"
$reportPath = Join-Path $PSScriptRoot "reports\selenium-report.html"

New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot "reports") -Force | Out-Null

Write-Host "Starting backend server..."
$backendProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --launch-profile https" -WorkingDirectory $backendPath -PassThru

try {
    $uri = [System.Uri]$BaseUrl
    $deadline = (Get-Date).AddSeconds(240)
    $isReady = $false

    while ((Get-Date) -lt $deadline) {
        if ($backendProcess.HasExited) {
            throw "Backend process exited early (code: $($backendProcess.ExitCode))."
        }

        Start-Sleep -Seconds 3
        try {
            $response = Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -UseBasicParsing -TimeoutSec 8
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                $isReady = $true
                break
            }
        }
        catch {
            # Fall back to a TCP port check because dev HTTPS certificates can cause request failures.
            try {
                $tcpClient = New-Object System.Net.Sockets.TcpClient
                $connectTask = $tcpClient.ConnectAsync($uri.Host, $uri.Port)
                if ($connectTask.Wait(1000) -and $tcpClient.Connected) {
                    $isReady = $true
                    $tcpClient.Close()
                    break
                }
                $tcpClient.Close()
            }
            catch {
                # Retry until timeout.
            }
        }
    }

    if (-not $isReady) {
        throw "Backend did not become reachable at $BaseUrl in time."
    }

    $env:BASE_URL = $BaseUrl
    $env:BROWSER = $Browser
    $env:E2E_USER_EMAIL = $Email
    $env:E2E_USER_PASSWORD = $Password
    $env:HEADLESS = if ($Headed) { "false" } else { "true" }

    Write-Host "Running Selenium tests with user: $Email"

    Push-Location $PSScriptRoot
    try {
        python -m pytest .\test_user_journeys.py -m userjourney --html=.\reports\selenium-report.html --self-contained-html
    }
    finally {
        Pop-Location
    }

    Write-Host "Selenium report generated at: $reportPath"
}
finally {
    if ($backendProcess -and -not $backendProcess.HasExited) {
        Write-Host "Stopping backend server..."
        Stop-Process -Id $backendProcess.Id -Force
    }
}
