// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Model
{
    public interface ITelemetryDataCollector
    {
        /// <summary>
        /// Accumulate data through out the life cycle of the data collector until PublishCumulativeTelemetryAsync is called
        /// This is used when the same data point is hit multiple times in an execution flow and firing an event each time
        /// is a costly operations. This helps is aggregating the data that would otherwise have been done later and the 
        /// individual data points themselves are of no interest.
        /// </summary>
        /// <param name="property">Name of the data point</param>
        /// <param name="value">Data point value to be stored</param>
        /// <param name="subArea">Sub area of the data point. This is along with the eventName forms the property name</param>
        void AddAndAggregate(string property, object value, string subArea = null);

        /// <summary>
        /// Adds a new property or overwrites it if it already exists. Accumulate data through out the life cycle of the data collector until 
        /// PublishCumulativeTelemetryAsync is called.
        /// </summary>
        /// <param name="property">Name of the data point</param>
        /// <param name="value">Data point value to be stored</param>
        /// <param name="subArea">Sub area of the data point. This is along with the eventName forms the property name</param>
        void AddOrUpdate(string property, object value, string subArea
            = null);

        /// <summary>
        /// Add an exception to the list of failures to telemetry
        /// <param name="exception">Exception occured.</param>
        /// </summary>
        void AddFailure(Exception exception);

        /// <summary>
        /// Publish the cumulative telemetry accrued over time and resets the collection
        /// </summary>
        Task PublishCumulativeTelemetryAsync();

        /// <summary>
        /// Publish a standalone event with custom properties
        /// <param name="feature">Feature name for the event</param>
        /// <param name="properties">Custom set of properties to publish</param>
        /// </summary>
        Task PublishTelemetryAsync(string feature, Dictionary<string, object> properties);

    }
}