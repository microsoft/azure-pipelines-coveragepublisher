// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class TraceLogger : ITraceLogger
    {
        #region Public Methods
        public TraceLogger(TraceListener traceListener)
        {
            _traceSource = new TraceSource("CodeCoveragePublisherTrace", SourceLevels.All);
            _traceSource.Listeners.Add(traceListener);
        }

        public virtual void Info(string text)
        {
            _traceSource.TraceEvent(TraceEventType.Information, 0, text);
        }

        public virtual void Warning(string text)
        {
            _traceSource.TraceEvent(TraceEventType.Warning, 0, text);
        }

        public virtual void Verbose(string text)
        {
            _traceSource.TraceEvent(TraceEventType.Verbose, 0, text);
        }

        public virtual void Error(string text)
        {
            _traceSource.TraceEvent(TraceEventType.Error, 0, text);
        }

        #endregion

        #region Private Members
        private readonly TraceSource _traceSource;
        #endregion
    }
}
