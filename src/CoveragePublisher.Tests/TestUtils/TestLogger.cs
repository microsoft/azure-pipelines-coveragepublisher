using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace CoveragePublisher.Tests
{
    class TestLogger : ILogger
    {
        public string Log { get; set; } = "";

        public void Debug(string message)
        {
            Log += "debug: " + message + Environment.NewLine;
        }

        public void Error(string message)
        {
            Log += "error: " + message + Environment.NewLine;
        }

        public void Info(string message)
        {
            Log += "info: " + message + Environment.NewLine;
        }

        public void Verbose(string message)
        {
            Log += "verbose: " + message + Environment.NewLine;
        }

        public void Warning(string message)
        {
            Log += "warning: " + message + Environment.NewLine;
        }
    }
}
