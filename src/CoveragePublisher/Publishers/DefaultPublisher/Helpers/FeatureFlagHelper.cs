// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.TeamFoundation.TestClient.PublishTestResults;
using Microsoft.VisualStudio.Services.FeatureAvailability.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    public class FeatureFlagHelper: IFeatureFlagHelper
    {
        private FeatureAvailabilityHttpClient _featureAvailabilityHttpClient;
        private FeatureAvailabilityHttpClient _featureAvailabilityTcmHttpClient;
        private IClientFactory _clientFactory;

        public FeatureFlagHelper(IClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public bool GetFeatureFlagState(string featureFlagName, bool isTcmFeature, bool errorIfNotFound = true)
        {
            try
            {
                var featureFlag = GetClient(isTcmFeature).GetFeatureFlagByNameAsync(featureFlagName).Result;
                if (featureFlag != null && featureFlag.EffectiveState.Equals("Off", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            catch
            {
                if(errorIfNotFound)
                {
                    TraceLogger.Error(string.Format(Resources.FailedToGetFeatureFlag, featureFlagName));
                }
                else
                {
                    TraceLogger.Warning(string.Format(Resources.FailedToGetFeatureFlag, featureFlagName));
                }
                return false;
            }
            return true;
        }

        private FeatureAvailabilityHttpClient GetClient(bool isTcmFeature)
        {
            if(isTcmFeature)
            {
                if(_featureAvailabilityTcmHttpClient == null)
                {
                    _featureAvailabilityTcmHttpClient = _clientFactory.GetClient<FeatureAvailabilityHttpClient>(TestLogStoreConstants.TCMServiceInstanceType);
                }
                return _featureAvailabilityTcmHttpClient;
            }
            else
            {
                if (_featureAvailabilityHttpClient == null)
                {
                    _featureAvailabilityHttpClient = _clientFactory.GetClient<FeatureAvailabilityHttpClient>();
                }
                return _featureAvailabilityHttpClient;
            }
        }
    }
}
