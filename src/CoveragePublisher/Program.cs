// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class Program
    {
        private static CancellationTokenSource _cancellationTokenSource;
        private static bool publishSuccess = false;

        public static void Main(string[] args)
        {
            var argsProcessor = new ArgumentsProcessor();
            var config = argsProcessor.ProcessCommandLineArgs(args);

            _cancellationTokenSource = new CancellationTokenSource();

            AppDomain.CurrentDomain.ProcessExit += ProcessExit;

            if (config != null)
            {
                ProcessCoverage(config, _cancellationTokenSource.Token);
            }
        }
        
        private static void ProcessCoverage(PublisherConfiguration config, CancellationToken cancellationToken)
        {
            // Currently the publisher only works for azure pipelines, so we simply instansiate for Azure Pipelines
            var context = AzurePipelinesPublisher.ExecutionContext;
            TraceLogger.Initialize(AzurePipelinesPublisher.ExecutionContext.Logger);

            AzurePipelinesPublisher publisher = null;

            try
            {
                publisher = new AzurePipelinesPublisher();
            }
            catch (Exception ex)
            {
                TraceLogger.Error(string.Format(Resources.CouldNotConnectToAzurePipelines, ex));
            }

            var processor = new CoverageProcessor(publisher);

            // By default wait for 2 minutes for coverage to publish
            var publishTimedout = processor.ParseAndPublishCoverage(config, cancellationToken, new Parser(config))
                                           .Wait(config.TimeoutInSeconds * 1000, cancellationToken);

            if(publishTimedout)
            {
                _cancellationTokenSource.Cancel();
            }
            else
            {
                publishSuccess = true;
            }
        }
        
        private static void ProcessExit(object sender, EventArgs e)
        {
            if (!publishSuccess)
            {
                _cancellationTokenSource.Cancel();
            }
        }
    }
}
