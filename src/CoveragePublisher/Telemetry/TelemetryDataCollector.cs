// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Services.CustomerIntelligence.WebApi;
using Microsoft.VisualStudio.Services.WebPlatform;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public class TelemetryDataCollector : ITelemetryDataCollector
    {
        private readonly ITraceLogger _logger;
        private readonly CustomerIntelligenceHttpClient _httpClient;
        private const string CumulativeTelemetryFeatureName = "ConsolidatedTelemetry";
        private readonly object _publishLockNode = new object();
        private readonly ConcurrentDictionary<string, object> _properties = new ConcurrentDictionary<string, object>();

        public string Area => "TestResultParser";

        public TelemetryDataCollector(IClientFactory clientFactory, ITraceLogger logger)
        {
            _logger = logger;
            _httpClient = clientFactory.GetClient<CustomerIntelligenceHttpClient>();
        }

        public virtual void AddOrUpdate(string property, object value, string subArea = null)
        {
            var propertyKey = !string.IsNullOrEmpty(subArea) ? $"{subArea}:{property}" : property;

            try
            {
                _properties[propertyKey] = value;
            }
            catch (Exception e)
            {
                _logger.Warning($"TelemetryDataCollector : AddOrUpdate : Failed to add {value} with key {propertyKey} due to {e}");
            }
        }

        /// <inheritdoc />
        public virtual void AddAndAggregate(string property, object value, string subArea = null)
        {
            var propertyKey = !string.IsNullOrEmpty(subArea) ? $"{subArea}:{property}" : property;

            try
            {
                // If key does not exist or aggregate option is false add value blindly
                if (!_properties.ContainsKey(propertyKey))
                {
                    _properties[propertyKey] = value;
                    return;
                }

                // If key exists and the value is a list, assume that existing value is a list and concat them
                if (value is IList list)
                {
                    foreach (var element in list)
                    {
                        (_properties[propertyKey] as IList)?.Add(element);
                    }
                    return;
                }

                // If key exists and is a list add new items to list
                if (_properties[propertyKey] is IList)
                {
                    ((IList) _properties[propertyKey]).Add(value);
                    return;
                }

                // If the key exists and value is integer or double arithmetically add them
                if (_properties[propertyKey] is int)
                {
                    _properties[propertyKey] = (int)_properties[propertyKey] + (int)value;
                }
                else if (_properties[propertyKey] is double)
                {
                    _properties[propertyKey] = (double)_properties[propertyKey] + (double)value;
                }
                else
                {
                    // If unknown type just blindly set value
                    _properties[propertyKey] = value;
                }
            }
            catch (Exception e)
            {
                _logger.Warning($"TelemetryDataCollector : AddAndAggregate : Failed to add {value} with key {propertyKey} due to {e}");
            }
        }

        public virtual Task PublishCumulativeTelemetryAsync()
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
                _logger.Verbose($"TelemetryDataCollector : PublishCumulativeTelemetryAsync : Failed to publish telemetry due to {e}");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual Task PublishTelemetryAsync(string feature, Dictionary<string, object> properties)
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
                _logger.Verbose($"TelemetryDataCollector : PublishTelemetryAsync : Failed to publish telemetry due to {e}");
            }

            return Task.CompletedTask;
        }

        public ConcurrentDictionary<string, object> Properties => _properties;
    }
}
