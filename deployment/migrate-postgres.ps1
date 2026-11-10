$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Default)) {
    throw "Set ConnectionStrings__Default in the deployment environment before applying migrations."
}

$env:Database__Provider = "Postgres"

Push-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "Could not restore the repository's EF Core tools." }

    dotnet tool run dotnet-ef database update --context AppDbContext
    if ($LASTEXITCODE -ne 0) { throw "PostgreSQL migration failed; do not start the app until it is resolved." }
}
finally {
    Pop-Location
}
