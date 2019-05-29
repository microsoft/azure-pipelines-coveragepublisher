// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using CommandLine;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    internal class ArgumentsProcessor
    {
        public class Options : ICLIArgs
        {
            [Value(0, Required = true, HelpText = "Set of coverage files to be published.")]
            public IEnumerable<string> CoverageFiles { get; set; }

            [Option("reportDirectory", Default = "", HelpText = "Path to report directory.")]
            public string ReportDirectory { get; set; }
        }


        public ICLIArgs ProcessCommandLineArgs(string[] args)
        {
            ICLIArgs cliArgs = null;

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts => {
                    cliArgs = opts;
                });

            return cliArgs;
        }
    }
}
