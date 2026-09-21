<#
.SYNOPSIS
    Sets up the Antigravity AI + Unity MCP configuration for this ProDomino project.

.DESCRIPTION
    This script configures the global Antigravity MCP config (~/.gemini/config/mcp_config.json)
    to point to the Unity MCP bridge server bundled in this repository.

    Run this ONCE after cloning the repo. It is optional — Antigravity also auto-discovers
    the project-level config at .agents/mcp.json (which uses a relative path).

    This global setup is useful if you want the Unity MCP tools available in ALL Antigravity
    sessions, not just when this project workspace is open.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\setup-antigravity.ps1
#>

$ErrorActionPreference = "Stop"

# Resolve paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir
$ServerJs  = Join-Path $RepoRoot ".agents\mcp\unity-bridge\server.js"

if (-not (Test-Path $ServerJs)) {
    Write-Host "ERROR: Could not find $ServerJs" -ForegroundColor Red
    Write-Host "Make sure you run this script from the repository root." -ForegroundColor Red
    exit 1
}

# Normalize to forward slashes for JSON
$ServerJsNormalized = $ServerJs.Replace('\', '/')

# Gemini config directory
$GeminiConfigDir = Join-Path $env:USERPROFILE ".gemini\config"
$McpConfigPath   = Join-Path $GeminiConfigDir "mcp_config.json"

# Create config directory if needed
if (-not (Test-Path $GeminiConfigDir)) {
    New-Item -ItemType Directory -Path $GeminiConfigDir -Force | Out-Null
    Write-Host "Created $GeminiConfigDir" -ForegroundColor Green
}

# Build the MCP config
$McpConfig = @{
    mcpServers = @{
        unity = @{
            command = "node"
            args    = @($ServerJsNormalized)
        }
    }
}

# If existing config exists, merge (preserve other MCP servers)
if (Test-Path $McpConfigPath) {
    try {
        $Existing = Get-Content $McpConfigPath -Raw | ConvertFrom-Json
        if ($Existing.mcpServers) {
            # Add/overwrite the "unity" entry, keep others
            $Existing.mcpServers | Add-Member -NotePropertyName "unity" -NotePropertyValue $McpConfig.mcpServers.unity -Force
            $McpConfig = $Existing
        }
    }
    catch {
        Write-Host "WARNING: Existing $McpConfigPath could not be parsed. Overwriting." -ForegroundColor Yellow
    }
}

# Write config
$JsonContent = $McpConfig | ConvertTo-Json -Depth 10
Set-Content -Path $McpConfigPath -Value $JsonContent -Encoding UTF8
Write-Host ""
Write-Host "=== Antigravity MCP Setup Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Global config written to:" -ForegroundColor White
Write-Host "  $McpConfigPath" -ForegroundColor Green
Write-Host ""
Write-Host "Unity MCP server path:" -ForegroundColor White
Write-Host "  $ServerJsNormalized" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Open the project in Unity Editor (the MCP bridge auto-starts)" -ForegroundColor White
Write-Host "  2. Open Antigravity — Unity MCP tools will be available" -ForegroundColor White
Write-Host "  3. Verify: In Unity, go to ProDomino > MCP > Check Status" -ForegroundColor White
Write-Host ""
