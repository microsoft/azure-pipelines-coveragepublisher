// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var argsProcessor = new ArgumentsProcessor();
            var publisherConfiguration = argsProcessor.ProcessCommandLineArgs(args);

            ConfigureLogging(publisherConfiguration);
        }

        private static void ConfigureLogging(PublisherConfiguration config)
        {
            if (config.TraceLogging)
            {
                var fileName = DateTime.UtcNow.ToString("CoveragePublisher.yyyy-MM-dd.HH-mm-ss." + Process.GetCurrentProcess().Id + ".log");
                var logFilePath = Path.Combine(Path.GetTempPath(), fileName);

                var listener = new TextWriterTraceListener(logFilePath);

                TraceLogger.Instance.AddListener(listener);
            }
        }
    }
}
