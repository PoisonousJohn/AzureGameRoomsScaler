using Microsoft.Azure.Management.Network.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureGameRoomsScaler
{
    public static class AzureAPIHelper
    {
        public static async Task DeallocateVMAsync(string VMID, string resourceGroup)
        {
            // not awaiting here intentionally, since we want to return response immediately
            var task = AzureMgmtCredentials.Instance.Azure.VirtualMachines.DeallocateAsync(resourceGroup, VMID);

            // we do not wait for VM deallocation
            // so let's give it few seconds to start and return in case we have deployment exception
            // like service principal error
            await Task.WhenAny(task, Task.Delay(1000));
            if (task.IsCompleted && task.IsFaulted)
            {
                throw task.Exception;
            }
        }

        public static async Task<string> GetVMPublicIP(string VMID, string resourceGroup)
        {
            var vm = await AzureMgmtCredentials.Instance.Azure.VirtualMachines.GetByResourceGroupAsync(resourceGroup, VMID);

            using (var client = new NetworkManagementClient(AzureMgmtCredentials.Instance.Credentials))
            {
                string networkInterfaceId = vm.NetworkInterfaceIds[0].Split('/').Last();
                client.SubscriptionId = AzureMgmtCredentials.Instance.SubscriptionId;
                var network =
                    await NetworkInterfacesOperationsExtensions.GetAsync(client.NetworkInterfaces,
                    resourceGroup, networkInterfaceId);
                string publicIPResourceId = network.IpConfigurations[0].PublicIPAddress.Id;

                var ip = await AzureMgmtCredentials.Instance.Azure.PublicIPAddresses.GetByIdAsync(publicIPResourceId);

                return ip.IPAddress;
            }

        }
    }
}
