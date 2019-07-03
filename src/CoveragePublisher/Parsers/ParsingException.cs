using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Parsers
{
    public class ParsingException: Exception
    {
        public ParsingException(string message, Exception innerException) : base(message, innerException) { }
    }
}
