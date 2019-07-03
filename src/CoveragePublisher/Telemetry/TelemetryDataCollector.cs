// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;

namespace Microsoft.Azure.Pipelines.CoveragePublisher
{
    public abstract class TelemetryDataCollector : ITelemetryDataCollector
    {
        protected const string CumulativeTelemetryFeatureName = "ConsolidatedTelemetry";
        protected readonly ConcurrentDictionary<string, object> _properties = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentBag<Exception> _failures = new ConcurrentBag<Exception>();

        public string Area => "TestResultParser";

        public bool TelemetryCollectionEnabled { get; private set; }

        public TelemetryDataCollector(bool telemetryCollectionEnabled)
        {
            TelemetryCollectionEnabled = telemetryCollectionEnabled;
            _properties["Failures"] = _failures;
        }

        public virtual void AddOrUpdate(string property, Func<object> value, string subArea = null)
        {
            if(TelemetryCollectionEnabled)
            {
                AddOrUpdate(property, value(), subArea);
            }
        }

        public virtual void AddOrUpdate(string property, object value, string subArea = null)
        {
            if (TelemetryCollectionEnabled)
            {
                var propertyKey = !string.IsNullOrEmpty(subArea) ? $"{subArea}:{property}" : property;

                try
                {
                    _properties[propertyKey] = value;
                }
                catch (Exception e)
                {
                    TraceLogger.Debug($"TelemetryDataCollector : AddOrUpdate : Failed to add {value} with key {propertyKey} due to {e}");
                }
            }
        }

        public virtual void AddAndAggregate(string property, Func<object> value, string subArea = null)
        {
            if(TelemetryCollectionEnabled)
            {
                AddAndAggregate(property, value(), subArea);
            }
        }

        public virtual void AddAndAggregate(string property, object value, string subArea = null)
        {
            if (TelemetryCollectionEnabled)
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
                        ((IList)_properties[propertyKey]).Add(value);
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
                    TraceLogger.Debug($"TelemetryDataCollector : AddAndAggregate : Failed to add {value} with key {propertyKey} due to {e}");
                }
            }
        }

        public void AddFailure(Exception exception)
        {
            if (TelemetryCollectionEnabled)
            {
                _failures.Add(exception);
                AddOrUpdate("FailureCount", _failures.Count);
            }
        }

        public abstract Task PublishCumulativeTelemetryAsync();

        public abstract Task PublishTelemetryAsync(string feature, Dictionary<string, object> properties);

        public ConcurrentDictionary<string, object> Properties => _properties;
    }
}
