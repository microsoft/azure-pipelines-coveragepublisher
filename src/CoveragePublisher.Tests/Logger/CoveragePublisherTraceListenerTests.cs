using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoveragePublisher.Tests
{
    [TestClass]
    public class CoveragePublisherTraceListenerTests
    {
        [TestMethod]
        public void WriteTests()
        {
            var listener = new CoveragePublisherTraceListener();
            var writer = new StringWriter();
            listener.Writer = writer;

            listener.Write("message1");
            listener.WriteLine("message2");

            var output = writer.ToString();

            Assert.IsTrue(Regex.IsMatch(output, @"[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}\.[0-9]{2}\.[0-9]{6} message1"));
            Assert.IsTrue(Regex.IsMatch(output, @"[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}\.[0-9]{2}\.[0-9]{6} message2" + Environment.NewLine));
        }
    }
}
