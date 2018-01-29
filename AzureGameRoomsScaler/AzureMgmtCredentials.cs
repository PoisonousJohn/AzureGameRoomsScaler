using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Network.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using System.Configuration;
using System.Threading.Tasks;
using System.Linq;

namespace AzureGameRoomsScaler
{
    public class AzureMgmtCredentials
    {
        public IAzure Azure { get; private set; }

        public string SubscriptionId { get; private set; }
        public AzureCredentials Credentials { get; private set; }


        public AzureMgmtCredentials()
        {
            SubscriptionId = ConfigurationManager.AppSettings["SubscriptionId"].ToString();
            var clientId = ConfigurationManager.AppSettings["ClientId"].ToString();
            var clientSecret = ConfigurationManager.AppSettings["ClientSecret"].ToString();
            var tenant = ConfigurationManager.AppSettings["Tenant"].ToString();
            Credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(
                clientId, clientSecret, tenant, AzureEnvironment.AzureGlobalCloud
            );
            Azure = Microsoft.Azure.Management.Fluent.Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(Credentials)
                .WithSubscription(SubscriptionId);
        }

        public static readonly AzureMgmtCredentials Instance = new AzureMgmtCredentials();
    }
}
