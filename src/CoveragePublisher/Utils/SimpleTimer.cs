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
        public SimpleTimer(string timerName, string telemetryArea, string telemetryEventName, ITraceLogger logger,
            ITelemetryDataCollector telemetryDataCollector, TimeSpan threshold, bool publishTelemetry = true)
        {
            _name = timerName;
            _telemetryEventName = telemetryEventName;
            _telemetryArea = telemetryArea;
            _logger = logger;
            _telemetry = telemetryDataCollector;
            _threshold = threshold;
            _timer = Stopwatch.StartNew();
            _publishTelemetry = publishTelemetry;
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

            if (_publishTelemetry)
            {
                _telemetry.AddAndAggregate(_telemetryEventName, _timer.Elapsed.TotalMilliseconds, _telemetryArea);
            }

            if (_timer.Elapsed > _threshold)
            {
                _logger.Warning($"PERF : {_name} : took {_timer.Elapsed.TotalMilliseconds} ms.");
            }
            else
            {
                _logger.Verbose($"PERF : {_name} : took {_timer.Elapsed.TotalMilliseconds} ms.");
            }
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
        private readonly ITraceLogger _logger;
        private readonly ITelemetryDataCollector _telemetry;
        private readonly Stopwatch _timer;
        private readonly string _name;
        private readonly string _telemetryEventName;
        private readonly string _telemetryArea;
        private readonly TimeSpan _threshold;
        private readonly bool _publishTelemetry;

        #endregion
    }
}
