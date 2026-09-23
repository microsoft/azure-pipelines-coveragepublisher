$root = Split-Path -Parent $PSScriptRoot

Describe 'Packaged PTR wrapper' {
    BeforeAll {
        $script:stub = Join-Path $TestDrive 'publisher stub.exe'
        Add-Type -Path (Join-Path $PSScriptRoot 'PublisherStub.cs') -OutputAssembly $script:stub -OutputType ConsoleApplication
        $script:reports = Join-Path $TestDrive 'reports with spaces'
        New-Item -ItemType Directory -Path $script:reports | Out-Null
        [IO.File]::WriteAllText((Join-Path $script:reports 'test results.trx'), '<TestRun />')
        $script:parameters = @{
            PublisherPath = $script:stub
            ResultsDirectory = $script:reports
            TempDirectory = $TestDrive
            CollectionUrl = 'https://dev.azure.com/example/'
            ProjectName = 'Example project'
            AccessToken = 'fake-test-token'
        }
        $script:original = @{}
        foreach ($name in @('PTR_TEST_EXIT_CODE', 'PTR_TEST_ERROR', 'PTR_TEST_SLEEP', 'buildid', 'builduri')) {
            $script:original[$name] = [Environment]::GetEnvironmentVariable($name)
        }
    }

    BeforeEach {
        foreach ($name in @('PTR_TEST_EXIT_CODE', 'PTR_TEST_ERROR', 'PTR_TEST_SLEEP')) {
            [Environment]::SetEnvironmentVariable($name, $null)
        }
        $env:buildid = '123'
        $env:builduri = 'must-not-reach-publisher'
    }

    AfterAll {
        foreach ($name in $script:original.Keys) {
            [Environment]::SetEnvironmentVariable($name, $script:original[$name])
        }
    }

    It 'extracts the original package with the required layout' {
        $destination = Join-Path $TestDrive 'extracted'
        $exe = & (Join-Path $root 'Prepare-Ptr.ps1') -Destination $destination
        Test-Path -LiteralPath $exe -PathType Leaf | Should Be $true
        Test-Path -LiteralPath (Join-Path $destination 'modules\TfsAssemblies\Newtonsoft.Json.dll') | Should Be $true
    }

    It 'rejects a modified package before extraction' {
        $badZip = Join-Path $TestDrive 'modified.zip'
        [IO.File]::WriteAllText($badZip, 'not the publisher')
        { & (Join-Path $root 'Prepare-Ptr.ps1') -Destination (Join-Path $TestDrive 'bad') -PackagePath $badZip } |
            Should Throw 'checksum mismatch'
    }

    It 'passes quoted report paths and required settings without inheriting ADO build IDs' {
        { & (Join-Path $root 'Invoke-Ptr.ps1') @script:parameters } | Should Not Throw
        @(Get-ChildItem -LiteralPath $TestDrive -Filter '*.rsp').Count | Should Be 0
        $env:buildid | Should Be '123'
    }

    It 'redacts tokens and neutralizes workflow commands in publisher output' {
        Mock Write-Host {}
        & (Join-Path $root 'Invoke-Ptr.ps1') @script:parameters
        Assert-MockCalled Write-Host -Times 1 -Exactly -ParameterFilter { $Object -eq '[PTR] ***' }
        Assert-MockCalled Write-Host -Times 1 -Exactly -ParameterFilter {
            $Object -eq '[PTR] ::error::This must remain ordinary prefixed output.'
        }
        Assert-MockCalled Write-Host -Times 0 -Exactly -ParameterFilter { $Object -eq '[PTR] fake-test-token' }
    }

    It 'rejects a missing results directory' {
        $parameters = $script:parameters.Clone()
        $parameters.ResultsDirectory = Join-Path $TestDrive 'absent'
        { & (Join-Path $root 'Invoke-Ptr.ps1') @parameters } | Should Throw 'does not exist'
    }

    It 'rejects empty report discovery' {
        $parameters = $script:parameters.Clone()
        $empty = Join-Path $TestDrive 'empty'
        New-Item -ItemType Directory -Path $empty | Out-Null
        $parameters.ResultsDirectory = $empty
        { & (Join-Path $root 'Invoke-Ptr.ps1') @parameters } | Should Throw 'No TRX files'
    }

    It 'rejects newline-containing tokens' {
        $parameters = $script:parameters.Clone()
        $parameters.AccessToken = "invalid`ntoken"
        { & (Join-Path $root 'Invoke-Ptr.ps1') @parameters } | Should Throw 'single-line'
    }

    It 'rejects Azure-agent fallback instead of reporting success' {
        $env:PTR_TEST_EXIT_CODE = '20000'
        { & (Join-Path $root 'Invoke-Ptr.ps1') @script:parameters } | Should Throw '20000'
        @(Get-ChildItem -LiteralPath $TestDrive -Filter '*.rsp').Count | Should Be 0
    }

    It 'fails on failed tests or failed publication' {
        $env:PTR_TEST_EXIT_CODE = '40000'
        { & (Join-Path $root 'Invoke-Ptr.ps1') @script:parameters } | Should Throw '40000'
    }

    It 'fails on other publisher errors' {
        $env:PTR_TEST_EXIT_CODE = '7'
        { & (Join-Path $root 'Invoke-Ptr.ps1') @script:parameters } | Should Throw 'code 7'
    }

    It 'fails on Azure task error messages even with exit zero' {
        $env:PTR_TEST_ERROR = 'true'
        { & (Join-Path $root 'Invoke-Ptr.ps1') @script:parameters } | Should Throw 'despite returning zero'
    }

    It 'terminates a timed-out publisher and removes its response file' {
        $env:PTR_TEST_SLEEP = 'true'
        $parameters = $script:parameters.Clone()
        $parameters.TimeoutSeconds = 1
        { & (Join-Path $root 'Invoke-Ptr.ps1') @parameters } | Should Throw 'timeout'
        @(Get-ChildItem -LiteralPath $TestDrive -Filter '*.rsp').Count | Should Be 0
    }
}
