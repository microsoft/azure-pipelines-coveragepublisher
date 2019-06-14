// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal class AzurePipelinesPublisher : ICoveragePublisher
    {
        private PipelinesExecutionContext _executionContext = new PipelinesExecutionContext();
        private ClientFactory _clientFactory;

        public AzurePipelinesPublisher()
        {
            _executionContext = new PipelinesExecutionContext();
            _clientFactory = new ClientFactory(new VssConnection(new Uri(_executionContext.CollectionUri), new VssBasicCredential("", _executionContext.AccessToken)));
        }

        public void PublishCoverageSummary(CoverageSummary coverageSummary)
        {

        }

        public void PublishFileCoverage(IList<FileCoverageInfo> coverageFiles)
        {
        }

        public void PublishHTMLReport(string reportDirectory)
        {
            var publisher = new HTMLReportPublisher(_executionContext, _clientFactory);

            publisher.PublishHTMLReportAsync(_executionContext, reportDirectory, new System.Threading.CancellationToken()).Wait();
        }
    }
}
