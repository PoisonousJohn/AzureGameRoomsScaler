using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using System.Configuration;

namespace AzureGameRoomsScaler
{
    public class AzureMgmtCredentials
    {
        //public const string ManagementURI = "https://management.core.windows.net/";
        //public const string BaseURL = "https://management.azure.com/";
        //public const string AuthURL = "https://login.windows.net/";
        //public const string GraphURL = "https://graph.windows.net/";

        public IAzure Azure { get; private set; }


        public AzureMgmtCredentials()
        {
            var subscriptionId = ConfigurationManager.AppSettings["SubscriptionId"].ToString();
            var clientId = ConfigurationManager.AppSettings["ClientId"].ToString();
            var clientSecret = ConfigurationManager.AppSettings["ClientSecret"].ToString();
            var tenant = ConfigurationManager.AppSettings["Tenant"].ToString();
            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(
                clientId, clientSecret, tenant, AzureEnvironment.AzureGlobalCloud
            );
            Azure = Microsoft.Azure.Management.Fluent.Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials)
                .WithSubscription(subscriptionId);
        }

        public static readonly AzureMgmtCredentials instance = new AzureMgmtCredentials();
    }
}
