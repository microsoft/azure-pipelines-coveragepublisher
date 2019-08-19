// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    internal class PipelinesLogger : ILogger
    {
        public void Verbose(string message)
        {
            Debug("Verbose: " + message);
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
