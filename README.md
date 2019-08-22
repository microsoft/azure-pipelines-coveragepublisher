[![Build Status](https://dev.azure.com/mseng/AzureDevOps/_apis/build/status/Teams/AutomatedTesting/microsoft.azure-pipelines-coveragepublisher?branchName=master)](https://dev.azure.com/mseng/AzureDevOps/_build/latest?definitionId=8880&branchName=master)

# azure-pipelines-coveragepublisher

Library for publishing coverage files in a build pipeline. This library currently integrates with [ReportGenerator.Core](https://www.nuget.org/packages/ReportGenerator.Core/) for parsing supported coverage files and creating HTML reports. This library shall be used for all future coverage publishing needs.

# Usage

Core library for coverage publisher is built for `.netstandard 2.0` and has console wrappers for both `netcore2.0` and `net461`

### Invoking

```ps1
# Simple invocation
dotnet CoveragePublisher.Console.dll /path/to/jacoco.xml

# Invocation with multiple files and/or formats.
dotnet CoveragePublisher.Console.dll /path/to/jacoco.xml /path/to/cobertura.xml
```

### CLI Options

| Option                | Description                                                |
| --------------------- | ---------------------------------------------------------- |
| --reportDirectory     | (Default: "") Path where html report will be generated.    |
| --sourceDirectory     | (Default: "") List of source directories separated by ';'. |
| --timeout             | (Default: 120) Timeout for CoveragePublisher in seconds.   |
| --noTelemetry         | (Default: false) Disable telemetry data collection.        |
| --help                | Display this help screen.                                  |

<br/>

# Build & Test

The source can be build through both Visual Studio and dotnet cli.

```
# Build
azure-pipelines-coveragepublisher/src$ dotnet build

# Test
azure-pipelines-coveragepublisher/src$ dotnet test
```

<br />

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
