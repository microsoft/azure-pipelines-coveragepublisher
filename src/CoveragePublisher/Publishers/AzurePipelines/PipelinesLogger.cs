using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    internal class PipelinesLogger : ILogger
    {

        public void Verbose(string message)
        {
            Console.WriteLine(message);
        }

        public void Info(string message)
        {
            Console.WriteLine(message);
        }

        public void Warning(string message)
        {
            Console.WriteLine("##vso[task.logissue type=warning]" + message);
        }

        public void Error(string message)
        {
            Console.WriteLine("##vso[task.logissue type=error]" + message);
        }

        public void Debug(string message)
        {
            Console.WriteLine("##vso[task.debug]" + message);
        }

    }
}
