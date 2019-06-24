using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    public interface IExecutionContext
    {
        ILogger Logger { get; }
    }
}
