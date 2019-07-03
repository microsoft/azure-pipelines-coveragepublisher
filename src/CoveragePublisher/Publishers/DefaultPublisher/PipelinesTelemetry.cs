// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher;
using Microsoft.VisualStudio.Services.CustomerIntelligence.WebApi;
using Microsoft.VisualStudio.Services.WebPlatform;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    public class PipelinesTelemetry : TelemetryDataCollector
    {
        private readonly object _publishLockNode = new object();
        private readonly CustomerIntelligenceHttpClient _httpClient;

        public PipelinesTelemetry(IClientFactory clientFactory, bool enableTelemetryCollection): base(enableTelemetryCollection)
        {
            _httpClient = clientFactory.GetClient<CustomerIntelligenceHttpClient>();
        }

        public override Task PublishCumulativeTelemetryAsync()
        {
            if (TelemetryCollectionEnabled)
            {
                try
                {
                    lock (_publishLockNode)
                    {
                        var ciEvent = new CustomerIntelligenceEvent
                        {
                            Area = Area,
                            Feature = CumulativeTelemetryFeatureName,
                            Properties = _properties.ToDictionary(entry => entry.Key, entry => entry.Value)
                        };

                        // This is to ensure that the single ci event is never fired more than once.
                        _properties.Clear();

                        return _httpClient.PublishEventsAsync(new[] { ciEvent });
                    }
                }
                catch (Exception e)
                {
                    TraceLogger.Debug($"TelemetryDataCollector : PublishCumulativeTelemetryAsync : Failed to publish telemetry due to {e}");
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task PublishTelemetryAsync(string feature, Dictionary<string, object> properties)
        {
            if (TelemetryCollectionEnabled)
            {
                try
                {
                    var ciEvent = new CustomerIntelligenceEvent
                    {
                        Area = Area,
                        Feature = feature,
                        Properties = properties
                    };

                    return _httpClient.PublishEventsAsync(new[] { ciEvent });
                }
                catch (Exception e)
                {
                    TraceLogger.Debug($"TelemetryDataCollector : PublishTelemetryAsync : Failed to publish telemetry due to {e}");
                }
            }

            return Task.CompletedTask;
        }

    }
}
