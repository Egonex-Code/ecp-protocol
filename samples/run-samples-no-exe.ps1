param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$sampleProjects = Get-ChildItem -Path $PSScriptRoot -Filter "*.csproj" -Recurse | Sort-Object FullName
if ($sampleProjects.Count -eq 0) {
    throw "No sample projects found under '$PSScriptRoot'."
}

Write-Host "Running samples with dotnet host (no direct .exe launch)..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host ""

foreach ($project in $sampleProjects) {
    Write-Host "=== $($project.BaseName) ===" -ForegroundColor Yellow

    dotnet build $project.FullName -c $Configuration -v:q
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for '$($project.FullName)'."
    }

    [xml]$projectXml = Get-Content -Path $project.FullName -Raw
    $targetFramework = $projectXml.Project.PropertyGroup.TargetFramework | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($targetFramework)) {
        $targetFrameworks = $projectXml.Project.PropertyGroup.TargetFrameworks | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($targetFrameworks)) {
            $targetFramework = ($targetFrameworks -split ';')[0]
        }
    }
    if ([string]::IsNullOrWhiteSpace($targetFramework)) {
        # Fallback for samples inheriting TFM from repository-level Directory.Build.props.
        $targetFramework = "net8.0"
    }

    $dllPath = Join-Path $project.Directory.FullName ("bin\" + $Configuration + "\" + $targetFramework + "\" + $project.BaseName + ".dll")
    if (-not (Test-Path $dllPath)) {
        throw "Expected output DLL not found: $dllPath"
    }

    dotnet $dllPath
    if ($LASTEXITCODE -ne 0) {
        throw "Execution failed for '$dllPath'."
    }

    Write-Host ""
}

Write-Host "All samples executed successfully." -ForegroundColor Green
