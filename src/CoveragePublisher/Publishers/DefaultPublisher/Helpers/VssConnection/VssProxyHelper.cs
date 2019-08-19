// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using Newtonsoft.Json;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    internal class VssProxyHelper
    {
        public static WebProxy GetProxy()
        {
            WebProxy proxy = null;

            if (ProxyConfiguration.GetProxyDetails() == null)
            {
                return proxy;
            }

            if (!string.IsNullOrWhiteSpace(ProxyConfiguration.GetProxyDetails().ProxyUrl)
                && !string.Equals("undefined", ProxyConfiguration.GetProxyDetails().ProxyUrl, StringComparison.OrdinalIgnoreCase)
                && !string.Equals("null", ProxyConfiguration.GetProxyDetails().ProxyUrl, StringComparison.OrdinalIgnoreCase))
            {
                proxy = new WebProxy(ProxyConfiguration.GetProxyDetails().ProxyUrl, true);

                if (!string.IsNullOrWhiteSpace(ProxyConfiguration.GetProxyDetails().ProxyUserName)
                    && !string.Equals("undefined", ProxyConfiguration.GetProxyDetails().ProxyUserName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals("null", ProxyConfiguration.GetProxyDetails().ProxyUserName, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(ProxyConfiguration.GetProxyDetails().ProxyPassword)
                    && !string.Equals("undefined", ProxyConfiguration.GetProxyDetails().ProxyPassword, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals("null", ProxyConfiguration.GetProxyDetails().ProxyPassword, StringComparison.OrdinalIgnoreCase))
                {
                    proxy.Credentials = new NetworkCredential(ProxyConfiguration.GetProxyDetails().ProxyUserName, ProxyConfiguration.GetProxyDetails().ProxyPassword);
                }

                if (!string.IsNullOrWhiteSpace(ProxyConfiguration.GetProxyDetails().ProxyBypassList)
                    && !string.Equals("undefined", ProxyConfiguration.GetProxyDetails().ProxyBypassList, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals("null", ProxyConfiguration.GetProxyDetails().ProxyBypassList, StringComparison.OrdinalIgnoreCase))
                {
                    proxy.BypassList = JsonConvert.DeserializeObject<string[]>(ProxyConfiguration.GetProxyDetails().ProxyBypassList);
                }

                proxy.BypassProxyOnLocal = false;
            }

            return proxy;
        }
    }
}
