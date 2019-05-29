// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    class Program
    {
        static void Main(string[] args)
        {
            var argsProcessor = new ArgumentsProcessor();

            var cliArgs = argsProcessor.ProcessCommandLineArgs(args);

            if (cliArgs != null)
            {
                Console.WriteLine(cliArgs.ReportDirectory);

                foreach(var input in cliArgs.CoverageFiles)
                {
                    Console.WriteLine(input);
                }
            }

            Console.ReadLine();
        }

    }
}
