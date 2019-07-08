using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    class VssSettingsConfiguration
    {
        public static VssClientHttpRequestSettings GetSettings()
        {
            var settings = VssClientHttpRequestSettings.Default.Clone();

            if (int.TryParse(Environment.GetEnvironmentVariable("VSTS_HTTP_RETRY") ?? string.Empty, out var maxRetryRequest))
            {
                settings.MaxRetryRequest = Math.Min(Math.Max(maxRetryRequest, settings.MaxRetryRequest), 10);
            }
            if (int.TryParse(Environment.GetEnvironmentVariable("VSTS_HTTP_TIMEOUT") ?? string.Empty, out var httpRequestTimeoutSeconds))
            {
                settings.SendTimeout = TimeSpan.FromSeconds(Math.Min(Math.Max(httpRequestTimeoutSeconds, settings.SendTimeout.Seconds), 1200));
            }

            return settings;
        }
    }
}
