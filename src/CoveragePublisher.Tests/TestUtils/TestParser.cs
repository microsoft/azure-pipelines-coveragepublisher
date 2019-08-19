// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.Azure.Pipelines.CoveragePublisher.Parsers;

namespace CoveragePublisher.Tests
{

    public class TestParser: Parser
    {
        private ICoverageParserTool _tool;

        public TestParser(PublisherConfiguration config, ITelemetryDataCollector telemetryDataCollector) : base(config, telemetryDataCollector) { }
        public TestParser(PublisherConfiguration config, ITelemetryDataCollector telemetryDataCollector, ICoverageParserTool tool) : base(config, telemetryDataCollector) {
            _tool = tool;
        }
        
        protected override void GenerateHTMLReport(ICoverageParserTool tool)
        {
            GenerateReport(tool);
        }

        public virtual void GenerateReport(ICoverageParserTool tool)
        {
            base.GenerateHTMLReport(tool);
        }

        protected override ICoverageParserTool GetCoverageParserTool(PublisherConfiguration config)
        {
            if(_tool != null)
            {
                return _tool;
            }

            return base.GetCoverageParserTool(config);
        }
    }
}
