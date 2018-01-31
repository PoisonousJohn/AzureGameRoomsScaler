using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace AzureGameRoomsScaler
{
    public static class MarkVMForDeallocation
    {
        /// <summary>
        /// Expects a vmName POST parameter
        /// Sets the vmName's state to MarkedForDeallocation
        /// However, if the game rooms count is zero, the VM will be deallocated ASAP
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>

        [FunctionName("MarkVMForDeallocation")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "node/deallocate")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("MarkVMForDeallocation Function was called");
            // Get POST body
            dynamic data = JsonConvert.DeserializeObject(await req.Content.ReadAsStringAsync());

            // Set name to query string or body data
            string vmName = data?.vmName;

            //trim it just in case
            vmName = vmName.Trim();

            var vm = await TableStorageHelper.Instance.GetVMByID(vmName);
            if (vm == null)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, $"VM {vmName} not found");
            }

            //set the state of the requested VM as MarkedForDeallocation
            vm.State = VMState.MarkedForDeallocation;

            //however, if there are no games running on this VM, deallocate it immediately
            if (vm.RoomsNumber == 0)
            {
                log.Info($"VM with ID {vmName} has zero game rooms so it will be deallocated immediately");
                await AzureAPIHelper.DeallocateVMAsync(vm.VMID, vm.ResourceGroup);
                vm.State = VMState.Deallocating;
            }

            //modify the VM state in the DB with the new value
            await TableStorageHelper.Instance.ModifyVMDetailsAsync(vm);

            return vmName == null
                ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a vmName in the request body")
                : req.CreateResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(vm));
        }
    }
}
