﻿using System;
using System.Diagnostics;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.TeamFoundation.TestClient.PublishTestResults;
using Microsoft.VisualStudio.Services.FeatureAvailability.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
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

        public bool GetFeatureFlagState(string featureFlagName, bool isTcmFeature)
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
                TraceLogger.Debug(string.Format(Resources.FailedToGetFeatureFlag, featureFlagName), TraceLevel.Error);
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
