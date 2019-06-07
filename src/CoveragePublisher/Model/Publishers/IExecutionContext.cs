using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    internal interface IExecutionContext
    {
        ILogger Logger { get; }
    }
}
