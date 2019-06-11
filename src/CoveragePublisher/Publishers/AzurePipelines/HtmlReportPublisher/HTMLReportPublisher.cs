using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal class HTMLReportPublisher
    {
        public async Task PublishHTMLReportAsync(PipelinesExecutionContext executionContext,string reportDirectory, CancellationToken cancellationToken)
        {
        }

        private async Task PublishCodeCoverageFilesAsync(PipelinesExecutionContext executionContext, List<Tuple<string, string>> files, bool browsable, CancellationToken cancellationToken)
        {
            var publishCCTasks = files.Select(async file =>
            {
            });

            await Task.WhenAll(publishCCTasks);
        }
    }
}
