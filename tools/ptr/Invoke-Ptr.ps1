[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PublisherPath,
    [Parameter(Mandatory = $true)]
    [string] $ResultsDirectory,
    [Parameter(Mandatory = $true)]
    [string] $TempDirectory,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^https://dev\.azure\.com/[A-Za-z0-9][A-Za-z0-9-]*/?$')]
    [string] $CollectionUrl,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ProjectName,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $AccessToken,
    [string] $RunTitle = 'GitHub PTR',
    [ValidateRange(1, 1800)]
    [int] $TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
if ($AccessToken -match '\s' -or $AccessToken.Contains([char]0)) {
    throw 'The ADO access token must be a nonempty, single-line token.'
}
$maskedToken = $AccessToken.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
Write-Host "::add-mask::$maskedToken"

$exe = (Get-Item -LiteralPath $PublisherPath).FullName
$temp = (Get-Item -LiteralPath $TempDirectory).FullName
if (-not (Test-Path -LiteralPath $ResultsDirectory -PathType Container)) {
    throw 'The test results directory does not exist.'
}
$files = @(Get-ChildItem -LiteralPath $ResultsDirectory -Recurse -File -Filter '*.trx' |
    Sort-Object FullName)
if ($files.Count -eq 0) {
    throw 'No TRX files were produced; no test run will be published.'
}
$paths = @($files | ForEach-Object { $_.FullName })
foreach ($path in @($exe, $temp) + $paths) {
    if ($path -match '["\r\n]') {
        throw 'Publisher and report paths cannot contain quotes or line breaks.'
    }
}

$responseFile = Join-Path $temp ("ptr-{0}.rsp" -f [guid]::NewGuid())
$process = $null
$start = $null
try {
    $quotedPaths = $paths | ForEach-Object { '"' + $_ + '"' }
    [IO.File]::WriteAllText($responseFile, "`r`n" + ($quotedPaths -join "`r`n"), [Text.UTF8Encoding]::new($false))
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $exe
    $start.Arguments = '"@' + $responseFile + '"'
    $start.WorkingDirectory = Split-Path -Parent $exe
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true

    # A GitHub run ID is not an ADO build ID. Do not inherit agent context.
    foreach ($name in @(
        'buildid', 'builduri', 'releaseuri', 'releaseenvironmenturi',
        'stagename', 'stageattempt', 'phasename', 'phaseattempt',
        'jobname', 'jobattempt', 'jobidentifier', 'owner', 'platform',
        'config', 'pullrequesttargetbranch', 'allowPtrToDetectTestRunRetryFiles',
        'ACTIONS_ID_TOKEN_REQUEST_TOKEN', 'GH_TOKEN', 'GITHUB_TOKEN'
    )) {
        $start.EnvironmentVariables.Remove($name)
    }
    $settings = @{
        accesstoken = $AccessToken
        collectionurl = $CollectionUrl
        projectname = $ProjectName
        testrunner = 'VSTest'
        mergeresults = 'true'
        publishrunattachments = 'true'
        failtaskonfailedtests = 'true'
        failtaskonfailuretopublishresults = 'true'
        testruntitle = $RunTitle
        testrunsystem = 'GitHubActions'
        agenttempdirectory = $temp
    }
    foreach ($name in $settings.Keys) {
        $start.EnvironmentVariables[$name] = $settings[$name]
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    if (-not $process.Start()) {
        throw 'The PTR process did not start.'
    }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill()
        $process.WaitForExit()
        throw "PTR exceeded its $TimeoutSeconds second timeout."
    }
    $output = $stdout.GetAwaiter().GetResult() + "`n" + $stderr.GetAwaiter().GetResult()
    $safeOutput = $output.Replace($AccessToken, '***')
    foreach ($line in ($safeOutput -split '\r\n|\n|\r')) {
        if ($line.Length -gt 0) {
            # Do not interpret publisher/test output as GitHub workflow commands.
            Write-Host "[PTR] $line"
        }
    }
    $exitCode = $process.ExitCode
    if ($exitCode -eq 20000) {
        throw 'PTR returned 20000: its EXE feature flag is disabled or could not be queried. The Azure-agent fallback is unavailable on GitHub. Inspect the preceding diagnostics.'
    }
    if ($exitCode -eq 40000) {
        throw 'PTR returned 40000: failed tests or failure to publish results. Inspect the preceding diagnostics and the ADO run.'
    }
    if ($exitCode -ne 0) {
        throw "PTR exited with code $exitCode. Inspect the preceding diagnostics for authentication, dependency or publishing errors."
    }
    if ($output -match '##vso\[task\.(logissue type=error|complete result=Failed)' -or
        $output -match '##\[error\]') {
        throw 'PTR emitted an Azure task error despite returning zero.'
    }
    Write-Host 'PTR returned zero. Confirm the run, results and attachments in ADO; this wrapper does not independently verify server state.'
}
finally {
    if ($null -ne $process) {
        $process.Dispose()
    }
    if ($null -ne $start) {
        $start.EnvironmentVariables.Remove('accesstoken')
    }
    if (Test-Path -LiteralPath $responseFile) {
        Remove-Item -LiteralPath $responseFile -Force
    }
}
