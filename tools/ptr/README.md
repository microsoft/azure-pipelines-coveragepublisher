# Packaged PTR Windows POC

`PublishTestResults.zip` is the unmodified public publisher archive referenced by
[PublishTestResultsV2/make.json](https://github.com/microsoft/azure-pipelines-tasks/blob/master/Tasks/PublishTestResultsV2/make.json):

https://testmanagementstore.z13.web.core.windows.net/testmanagementptrcontainer/30984522/PublishTestResults.zip

The executable is `modules\TestResultsPublisher.exe`. Its configuration and
`modules\TfsAssemblies` must remain beside it. `Prepare-Ptr.ps1` checks the pinned
SHA-256 before extracting the entire archive; it does not download replacement
DLLs or bypass the publisher's feature flags.

## Runner

- The workflow uses the GitHub-hosted `windows-2022` image. No self-hosted runner
  registration is required.
- The image provides .NET Framework 4.8, Windows PowerShell, Git and Azure CLI.
- The workflow installs .NET SDK 7 for the existing `net7.0` tests. .NET 7 is out
  of support; this is POC tooling, not a recommended production baseline.
- Publishing requires access to Entra, ADO and any attachment storage endpoints.

## Trigger

The workflow is `.github/workflows/ptr-windows.yml`. It reuses the identity and
ADO target already present in `verify-ado-auth.yml`, without changing that workflow.
The existing app must trust the selected repository/ref and have ADO publishing
permissions. No tokens are committed, written into the response file, or stored
in the job environment file.

Push a commit to `kathans/gh-tcm-oidc-poc` with the workflow and package present.
This uses the existing trusted ref; no PR or merge into `master` is required.
The job deliberately rejects other repositories/refs. Push using an account
authorized to write to this repository:

```powershell
git push origin kathans/gh-tcm-oidc-poc
```

Watch Actions > Publish TRX with packaged PTR (Windows POC). Branch pushes also
trigger the pre-existing authentication/sample-publishing and JWT-demo workflows.
Each publishing workflow can create a new ADO test run.

The job generates real TRX files with the existing test project, obtains a fresh
ADO token, and invokes the EXE with a response file. Failed tests still allow
publication, while preserving the failed test step/job status. Per-job files and
the isolated Azure CLI cache are removed at the end, and GitHub disposes of the
hosted VM after the job.

## Observed publication

This is a wrapper around the existing Windows EXE, not a new cross-platform host.
The first authenticated Windows run succeeded on 2026-09-09 (UTC):

- [GitHub run 34392107301](https://github.com/microsoft/azure-pipelines-coveragepublisher/actions/runs/34392107301)
  used the existing federation and the packaged EXE.
- [ADO run 5297464](https://dev.azure.com/tfspfcusctest/TestAI/_testManagement/runs?_a=runSummary&runId=5297464)
  is completed with 66 results, all passed, and no associated ADO build.
- ADO exposes one run attachment: `TestResults_5297464.zip`.
- The LogStore upload returned a Bad Request warning. The publisher fell back
  to FileService, and the attachment was confirmed through the ADO attachment API.

This establishes the exercised TRX/run-attachment path, not every parser format,
result-level attachment case or target organization.

## Known limitations

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
