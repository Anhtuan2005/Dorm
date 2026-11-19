param(
    [ValidateSet('Vnpay', 'Realtime', 'Chatbot', 'AccountLock', 'HousingSupplement', 'RoomsAndBeds', 'Audit', 'Residence', 'PlanCompletions', 'Core', 'Billing')][string]$Scenario = 'Vnpay',
    [string]$AssemblyPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$webRoot = Join-Path $projectRoot 'DormitoryManagementSystem'
if ($AssemblyPath) {
    $webAssembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
} else {
    $webAssembly = Join-Path $webRoot 'bin\Release\net8.0\DormitoryManagementSystem.dll'
}
if (-not (Test-Path -LiteralPath $webAssembly -PathType Leaf)) { throw 'Build Release before running the browser smoke.' }

$runId = [Guid]::NewGuid().ToString('N')
$fixtureDb = "DormitoryManagementSystem_Test_$runId"
$instance = 'DESKTOP-OSSBOT1\TUANANH'
$vnpayProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$vnpayProbe.Start()
$port = $vnpayProbe.LocalEndpoint.Port
$vnpayProbe.Stop()

$origin = "http://127.0.0.1:$port"
$logDir = (New-Item -Type Directory -Path (Join-Path $projectRoot "artifacts\browser-fixtures\$runId") -Force).FullName
$stdout = Join-Path $logDir 'stdout.log'
$stderr = Join-Path $logDir 'stderr.log'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $origin
$env:ConnectionStrings__DefaultConnection = "Server=$instance;Database=$fixtureDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
$env:DemoData__Seed = 'true'
$env:DemoData__Password = "Fixture1!$runId"
$env:Chatbot__ApiKey = if ($Scenario -eq 'Chatbot') { ' ' } else { 'fixture-only' }
if ($Scenario -eq 'Chatbot') {
    $env:StudentSupport__Office = 'KTX Fixture'
    $env:StudentSupport__Phone = '+1 202 555 0142'
    $env:StudentSupport__Email = 'support@example.test'
}
$env:DORM_E2E_PASSWORD = $env:DemoData__Password
$env:DORM_E2E_BASE_URL = $origin
$env:DORM_VNPAY_FIXTURE_DB = $fixtureDb
$env:DORM_BROWSER_FIXTURE_DB = $fixtureDb
$env:DORM_BROWSER_FIXTURE_ARTIFACTS = $logDir
$env:Vnpay__Enabled = if ($Scenario -eq 'Vnpay') { 'true' } else { 'false' }
$env:Vnpay__MerchantCode = 'TESTKTX1'
$env:Vnpay__HashSecret = "fixture-only-$runId"
$env:Vnpay__PublicBaseUrl = $origin
$vnpayApp = $null

try {
    $vnpayApp = Start-Process -FilePath (Get-Command dotnet).Source -WorkingDirectory $webRoot `
        -ArgumentList ('"{0}"' -f $webAssembly) -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $healthy = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while (-not $healthy -and -not $vnpayApp.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        try { $healthy = (Invoke-RestMethod "$origin/health" -TimeoutSec 2).status -eq 'Healthy' }
        catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $healthy) {
        Get-Content -LiteralPath $stdout -Tail 18
        Get-Content -LiteralPath $stderr -Tail 18
        throw 'The disposable browser fixture did not start.'
    }
    $testScript = switch ($Scenario) {
        'Vnpay' { Join-Path $PSScriptRoot 'smoke.cjs' }
        'Realtime' { Join-Path $projectRoot '.scratch\realtime-notifications\smoke.cjs' }
        'Chatbot' { Join-Path $projectRoot '.scratch\chatbot-knowledge\smoke.cjs' }
        'AccountLock' { Join-Path $projectRoot 'scripts\smoke-account-lock.cjs' }
        'HousingSupplement' { Join-Path $projectRoot 'scripts\smoke-housing-supplement.cjs' }
        'RoomsAndBeds' { Join-Path $projectRoot 'scripts\smoke-rooms-and-beds.cjs' }
        'Audit' { Join-Path $projectRoot 'scripts\audit-final-system.cjs' }
        'PlanCompletions' { Join-Path $projectRoot 'scripts\smoke-plan-completions.cjs' }
        'Core' { Join-Path $projectRoot 'scripts\smoke-core-completions.cjs' }
        'Residence' { Join-Path $projectRoot 'scripts\smoke-residence.cjs' }
        'Billing' { Join-Path $projectRoot 'scripts\smoke-billing-ledger.cjs' }
    }
    node $testScript
    if ($LASTEXITCODE -ne 0) { throw "The $Scenario browser smoke failed." }
} finally {
    if ($vnpayApp -and -not $vnpayApp.HasExited) {
        Stop-Process -InputObject $vnpayApp -Force
        [void]$vnpayApp.WaitForExit(10000)
    }
    if ($fixtureDb -notmatch '^DormitoryManagementSystem_Test_[a-f0-9]{32}$') { throw 'Unsafe fixture database name.' }
    sqlcmd -S $instance -d master -E -C -b -l 5 -Q "IF DB_ID(N'$fixtureDb') IS NOT NULL BEGIN ALTER DATABASE [$fixtureDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$fixtureDb]; END"
    if ($LASTEXITCODE -ne 0) { throw 'The disposable browser database could not be removed.' }
}
