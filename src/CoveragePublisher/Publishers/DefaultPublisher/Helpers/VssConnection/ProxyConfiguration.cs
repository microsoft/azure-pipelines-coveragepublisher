// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    internal class ProxyConfiguration
    {
        private static ProxyConfiguration _proxyInstance;
        private static readonly object SyncLock = new Object();

        public string ProxyUrl { get; private set; }

        public string ProxyUserName { get; private set; }

        public string ProxyPassword { get; private set; }

        public string ProxyBypassList { get; private set; }

        protected ProxyConfiguration(string proxyUrl, string proxyUserName, string proxyPassword, string proxyBypassList)
        {
            ProxyUrl = proxyUrl;
            ProxyUserName = proxyUserName;
            ProxyPassword = proxyPassword;
            ProxyBypassList = proxyBypassList;
        }
        
        public static ProxyConfiguration GetProxyDetails()
        {
            if(_proxyInstance == null)
            {
                _proxyInstance = new ProxyConfiguration(Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ProxyConfiguration.ProxyUrl),
                                     Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ProxyConfiguration.ProxyUserName),
                                     Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ProxyConfiguration.ProxyPassword),
                                     Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ProxyConfiguration.ProxyByPassList));
            }

            return _proxyInstance;
        }
    }
}
