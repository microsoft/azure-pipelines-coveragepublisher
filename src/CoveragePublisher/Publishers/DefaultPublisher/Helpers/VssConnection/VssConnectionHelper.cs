using System;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.Azure.Pipelines.CoveragePublisher.Publishers.DefaultPublisher
{
    public class VssConnectionHelper
    {
        public static string ConnectionUrl { get; set; }
        public static VssBasicCredential Credential { get; } = new VssBasicCredential();
        private static VssConnectionHelper _instance;
        private static readonly object conLock = new object();

        public VssConnectionHelper(VssConnection vssConnection)
        {
            _vssConnection = vssConnection;
        }

        public static VssConnectionHelper Instance
        {
            get
            {
                lock (conLock)
                {
                    if (_instance != null)
                    {
                        return _instance;
                    }

                    var proxy = VssProxyHelper.GetProxy();
                    if (proxy != null)
                    {
                        VssHttpMessageHandler.DefaultWebProxy = proxy;
                    }

                    var connectionSettings = VssSettingsConfiguration.GetSettings();

                    // By default if null/empty/other than PAT -> JWT type
                    var connection = string.Equals(ConnectionConstants.PersonalAccessTokenType, Credential.AuthType, StringComparison.OrdinalIgnoreCase)
                        ? new VssConnection(new Uri(ConnectionUrl), new VssBasicCredential(Credential.AuthUser, Credential.AccessToken), connectionSettings)
                        : new VssConnection(new Uri(ConnectionUrl), new VssOAuthAccessTokenCredential(Credential.AccessToken), connectionSettings);

                    _instance = new VssConnectionHelper(connection);
                    return _instance;
                }
            }
            set
            {
                _instance = value;
            }
        }

        public VssConnection GetConnection()
        {
            return _vssConnection;
        }

        private readonly VssConnection _vssConnection;
    }
}