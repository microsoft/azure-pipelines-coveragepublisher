// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Utils
{
    internal class SimpleTimer : IDisposable
    {
        /// <summary>
        /// Creates a timer with threshold. A perf message is logged only if
        /// the time elapsed is more than the threshold.
        /// </summary>
        public SimpleTimer(string telemetryArea, string telemetryEventName, ITelemetryDataCollector telemetryDataCollector)
        {
            _telemetryEventName = telemetryEventName;
            _telemetryArea = telemetryArea;
            _telemetry = telemetryDataCollector;
            _timer = Stopwatch.StartNew();
        }

        /// <summary>
        /// Implement IDisposable
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Stop the watch and log the trace message with the elapsed time.
        /// Additionally also adds the elapsed time to telemetry under the timer name
        /// </summary>
        public void StopAndLog()
        {
            _timer.Stop();

            _telemetry.AddAndAggregate(_telemetryEventName, _timer.Elapsed.TotalMilliseconds, _telemetryArea);
            TraceLogger.Debug($"PERF : {_telemetryArea}.{_telemetryEventName} : took {_timer.Elapsed.TotalMilliseconds} ms.");

        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                StopAndLog();
            }

            _disposed = true;
        }

        #region private variables.

        private bool _disposed;
        private readonly ITelemetryDataCollector _telemetry;
        private readonly Stopwatch _timer;
        private readonly string _telemetryEventName;
        private readonly string _telemetryArea;

        #endregion
    }
}
