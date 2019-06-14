using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace CoveragePublisher.L1.Tests
{
    class TestLogger : ILogger
    {
        public string Log { get; set; }

        public void Debug(string message)
        {
            Log += message + Environment.NewLine;
        }

        public void Error(string message)
        {
            Log += message + Environment.NewLine;
        }

        public void Info(string message)
        {
            Log += message + Environment.NewLine;
        }

        public void Verbose(string message)
        {
            Log += message + Environment.NewLine;
        }

        public void Warning(string message)
        {
            Log += message + Environment.NewLine;
        }
    }
}
