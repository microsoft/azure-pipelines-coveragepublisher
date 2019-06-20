using System;
using Microsoft.Azure.Pipelines.CoveragePublisher.Model;
using Microsoft.VisualStudio.Services.FeatureAvailability.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.AzurePipelines
{
    public class FeatureFlagHelper: IFeatureFlagHelper
    {
        FeatureAvailabilityHttpClient _featureAvailabilityHttpClient;
        public FeatureFlagHelper(IClientFactory clientFactory)
        {
            FeatureAvailabilityHttpClient featureAvailabilityHttpClient = clientFactory.GetClient<FeatureAvailabilityHttpClient>();
        }

        public bool GetFeatureFlagState(string FFName, ILogger logger)
        {
            try
            {
                var featureFlag = _featureAvailabilityHttpClient.GetFeatureFlagByNameAsync(FFName).Result;
                if (featureFlag != null && featureFlag.EffectiveState.Equals("Off", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            catch
            {
                logger.Debug(string.Format(Resources.FailedToGetFeatureFlag, FFName));
                return true;
            }
            return true;
        }
    }
}
