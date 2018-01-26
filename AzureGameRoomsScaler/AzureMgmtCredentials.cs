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

        private string subscriptionId;
        private AzureCredentials credentials;

        public AzureMgmtCredentials()
        {
            subscriptionId = ConfigurationManager.AppSettings["SubscriptionId"].ToString();
            var clientId = ConfigurationManager.AppSettings["ClientId"].ToString();
            var clientSecret = ConfigurationManager.AppSettings["ClientSecret"].ToString();
            var tenant = ConfigurationManager.AppSettings["Tenant"].ToString();
            credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(
                clientId, clientSecret, tenant, AzureEnvironment.AzureGlobalCloud
            );
            Azure = Microsoft.Azure.Management.Fluent.Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials)
                .WithSubscription(subscriptionId);
        }

        public static readonly AzureMgmtCredentials instance = new AzureMgmtCredentials();


        public async Task<string> GetVMPublicIP(string VMID, string resourceGroup)
        {
            var vm = await Azure.VirtualMachines.GetByResourceGroupAsync(resourceGroup, VMID);

            using (var client = new NetworkManagementClient(credentials))
            {
                string networkInterfaceId = vm.NetworkInterfaceIds[0].Split('/').Last();
                client.SubscriptionId = subscriptionId;
                var network =
                    await NetworkInterfacesOperationsExtensions.GetAsync(client.NetworkInterfaces,
                    resourceGroup, networkInterfaceId);
                string publicIPResourceId = network.IpConfigurations[0].PublicIPAddress.Id;

                var ip = await Azure.PublicIPAddresses.GetByIdAsync(publicIPResourceId);

                return ip.IPAddress;
            }

        }
    }
}
