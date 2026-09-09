# Packaged PTR Windows POC

`PublishTestResults.zip` is the unmodified public publisher archive referenced by
[PublishTestResultsV2/make.json](https://github.com/microsoft/azure-pipelines-tasks/blob/master/Tasks/PublishTestResultsV2/make.json):

https://testmanagementstore.z13.web.core.windows.net/testmanagementptrcontainer/30984522/PublishTestResults.zip

The executable is `modules\TestResultsPublisher.exe`. Its configuration and
`modules\TfsAssemblies` must remain beside it. `Prepare-Ptr.ps1` checks the pinned
SHA-256 before extracting the entire archive; it does not download replacement
DLLs or bypass the publisher's feature flags.

## Runner requirements

- An approved Windows x64 self-hosted runner with label `ptr-windows`.
- .NET Framework 4.8 or later, Windows PowerShell 5.1, Git and Azure CLI.
- Access to GitHub, NuGet, Entra, ADO and any attachment storage endpoints.
- The workflow installs .NET SDK 7 for the existing `net7.0` tests. .NET 7 is out
  of support; this is POC tooling, not a recommended production baseline.

This repository is public. Do not register a workstation or expose a persistent
corporate machine to untrusted pull requests. Use an organization-approved
isolated, disposable runner with enforced access restrictions. Labels only route
jobs; they are not a security boundary. Remove/destroy the runner after use.

## Manual run

The workflow is `.github/workflows/ptr-self-hosted.yml`. It reuses the identity and
ADO target already present in `verify-ado-auth.yml`, without changing that workflow.
The existing app must trust the selected repository/ref and have ADO publishing
permissions. No tokens are committed, written into the response file, or stored
in the job environment file.

For manual dispatch, a maintainer must merge the workflow into the default branch
(`master`), and the same files/package must be available on
`kathans/gh-tcm-oidc-poc`. The job deliberately rejects other repositories/refs.
Then use Actions > Publish TRX with packaged PTR (Windows POC) > Run workflow and
select `kathans/gh-tcm-oidc-poc`, or:

```powershell
gh workflow run ptr-self-hosted.yml --repo microsoft/azure-pipelines-coveragepublisher --ref kathans/gh-tcm-oidc-poc
```

The runner must be online with all requested labels. The job generates real TRX
files with the existing test project, obtains an ADO token, and invokes the EXE
with a response file. Failed tests still allow publication, while preserving the
failed test step/job status. Per-job files and the isolated Azure CLI cache are
removed at the end; machine disposal is still required to ensure cleanup after
forced termination or compromise.

## Known limitations

This is a wrapper around the existing Windows EXE, not a new cross-platform host.
End-to-end publication from this workflow has not been established.

- Exit `20000`: the EXE selection feature flag is off or its query failed.
  Inspect authentication/dependency/feature-flag diagnostics. There is no Azure
  agent to handle the fallback, so the workflow fails.
- Exit `40000`: failed tests or a publishing failure. Inspect the logs and ADO.
- Other nonzero exits or Azure task error messages fail the workflow.
- The ZIP does not contain every transitive assembly reference. If the executed
  path requires an absent assembly, rebuild the publisher package with matching
  dependencies rather than copying arbitrary DLL versions.
- The EXE still constructs pipeline context internally. GitHub build/run IDs are
  not passed as ADO IDs. ADO rejection of build-less context requires a host change.
- A zero exit is not independent proof of server state. Confirm the ADO test run,
  result counts and expected attachments after the first authenticated run.

The wrapper masks the token, prefixes publisher output so it cannot issue GitHub
workflow commands, treats Azure task error markers as failures, and enforces a
publisher timeout. It does not install or run the Azure Pipelines agent.

## Local wrapper tests

Use the existing Windows PowerShell Pester installation:

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script .\tools\ptr\tests\Ptr.Tests.ps1 -EnableExit"
```

The tests compile a local stub using .NET Framework and do not contact ADO.
