using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal class HTMLReportPublisher
    {
        private PipelinesExecutionContext _executionContext;
        private ClientFactory _clientFactory;

        public HTMLReportPublisher(PipelinesExecutionContext executionContext, ClientFactory clientFactory)
        {
            _executionContext = executionContext;
            _clientFactory = clientFactory;
        }

        public async Task PublishHTMLReportAsync(PipelinesExecutionContext executionContext, string reportDirectory, CancellationToken cancellationToken)
        {
         
        }

        private async Task PublishCodeCoverageFilesAsync(PipelinesExecutionContext executionContext, List<Tuple<string, string>> files, bool browsable, CancellationToken cancellationToken)
        {

        }
    }
}
