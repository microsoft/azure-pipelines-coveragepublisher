using Microsoft.Azure.Pipelines.CoveragePublisher;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CoveragePublisher.L0.Tests
{
    [TestClass]
    public class ProgramTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void MainTestWithZeroArgs()
        {
            Program.Main(null);
        }
    }
}
