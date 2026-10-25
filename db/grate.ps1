<#
.SYNOPSIS
  Database migrations for School Calendar, powered by grate (https://erikbra.github.io/grate/).

.EXAMPLE
  ./db/grate.ps1 migrate   # Apply every new or changed script (safe to run repeatedly)
  ./db/grate.ps1 status    # Is the database up to date? (exit 0 = yes, 2 = changes pending)
  ./db/grate.ps1 dryrun    # Show what would run without touching the database
  ./db/grate.ps1 watch     # Migrate now, then again every time a .sql file changes (local development)
  ./db/grate.ps1 reset     # Drop and rebuild the database from scratch (LOCAL environment only)

  Configuration: $env:DB_CONNECTION, $env:DB_ENV (default LOCAL), $env:DB_TYPE (default sqlserver),
  $env:DB_SQL_DIR (default db/sql), $env:WATCH_INTERVAL.
#>
param(
    [ValidateSet('migrate', 'status', 'dryrun', 'watch', 'reset')]
    [string]$Command = 'migrate'
)
$ErrorActionPreference = 'Stop'

$DbDir = $PSScriptRoot
$SqlDir = if ($env:DB_SQL_DIR) { $env:DB_SQL_DIR } else { Join-Path $DbDir 'sql' }
$Connection = if ($env:DB_CONNECTION) { $env:DB_CONNECTION } else { 'Server=localhost,1433;Database=SchoolCalendar;User Id=sa;Password=Local_Dev_Passw0rd!;TrustServerCertificate=True' }
$Environment = if ($env:DB_ENV) { $env:DB_ENV } else { 'LOCAL' }
$DbType = if ($env:DB_TYPE) { $env:DB_TYPE } else { 'sqlserver' }
$OutputDir = if ($env:GRATE_OUTPUT) { $env:GRATE_OUTPUT } else { Join-Path $DbDir '.grate' }

function Invoke-Grate([string[]]$Extra) {
    Push-Location $DbDir
    try {
        # The tool version is pinned in /dotnet-tools.json; `dotnet tool restore` installs it.
        & dotnet tool run grate -- --connectionstring $Connection --files $SqlDir --databasetype $DbType `
            --environment $Environment --output $OutputDir --silent @Extra | Out-Host
        return $LASTEXITCODE
    } finally { Pop-Location }
}

function Get-Snapshot {
    # Hash of every script's path + content; changes when any .sql file is added, edited, renamed or deleted.
    $parts = Get-ChildItem $SqlDir -Recurse -Filter *.sql -File | Sort-Object FullName |
        ForEach-Object { $_.FullName + ':' + (Get-FileHash $_.FullName -Algorithm SHA1).Hash }
    ($parts -join "`n").GetHashCode()
}

switch ($Command) {
    'migrate' { exit (Invoke-Grate @('--transaction')) }
    'dryrun'  { exit (Invoke-Grate @('--dryrun')) }
    'status' {
        Push-Location $DbDir
        try {
            $out = & dotnet tool run grate -- --connectionstring $Connection --files $SqlDir --databasetype $DbType `
                --environment $Environment --output $OutputDir --silent --isuptodate 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { Write-Output $out; exit 1 }
        } finally { Pop-Location }
        if ($out -match 'Up to date: True') { Write-Output 'Database is up to date.'; exit 0 }
        Write-Output 'Database has pending changes. Run: ./db/grate.ps1 migrate'; exit 2
    }
    'watch' {
        Write-Output "Watching $SqlDir for changes (Ctrl+C to stop)..."
        $interval = if ($env:WATCH_INTERVAL) { [int]$env:WATCH_INTERVAL } else { 2 }
        $last = $null
        while ($true) {
            $current = Get-Snapshot
            if ($current -ne $last) {
                if ($null -ne $last) { Write-Output "$(Get-Date -Format T) Change detected, migrating..." }
                if ((Invoke-Grate @('--transaction')) -eq 0) { Write-Output "$(Get-Date -Format T) Database is up to date." }
                else { Write-Output "$(Get-Date -Format T) Migration failed; fix the script and save again." }
                $last = $current
            }
            Start-Sleep -Seconds $interval
        }
    }
    'reset' {
        if ($Environment -ne 'LOCAL') { throw "Refusing to drop a database outside the LOCAL environment (DB_ENV=$Environment)." }
        exit (Invoke-Grate @('--drop'))
    }
}
