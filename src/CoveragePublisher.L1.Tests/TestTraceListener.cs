using System;
using System.Diagnostics;

namespace CoveragePublisher.L1.Tests
{
    class TestTraceListener : TextWriterTraceListener
    {
        public string Log { get; private set; } = "";

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
