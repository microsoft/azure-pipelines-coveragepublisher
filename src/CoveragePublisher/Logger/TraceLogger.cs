// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class TraceLogger : ITraceLogger
    {

        public static TraceLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TraceLogger();
                }

                return _instance;
            }
        }

        #region Public Methods

        public static void ResetLogger()
        {
            _instance = null;
        }

        public void AddListener(TraceListener traceListener)
        {
            _traceSource.Listeners.Add(traceListener);
        }

        public void Info(string message)
        {
            _traceSource.TraceEvent(TraceEventType.Information, 0, message);
        }

        public void Warning(string message)
        {
            _traceSource.TraceEvent(TraceEventType.Warning, 0, message);
        }

        public void Verbose(string message)
        {
            _traceSource.TraceEvent(TraceEventType.Verbose, 0, message);
        }

        public void Error(string message)
        {
            _traceSource.TraceEvent(TraceEventType.Error, 0, message);
        }

        #endregion

        #region Private Methods

        private TraceLogger()
        {
            _traceSource = new TraceSource("CodeCoveragePublisherTrace", SourceLevels.All);
        }

        #endregion

        #region Private Members
        private static TraceLogger _instance;
        private readonly TraceSource _traceSource;
        #endregion
    }
}
