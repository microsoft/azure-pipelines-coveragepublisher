using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Azure.Pipelines.CoveragePublisher;

namespace CoveragePublisher.L0.Tests
{
    class TestTraceListener : CoveragePublisherTraceListener
    {
        public string Log { get; set; } = "";

        public override void Write(string message)
        {
            Log += message;
        }

        public override void WriteLine(string message)
        {
            Log += message + Environment.NewLine;
        }
    }
}
