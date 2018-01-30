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
        /// Marks the VM's state as "MarkedForDeallocation" on the DB. Performs *NO* operation on the VM itself.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("MarkVMForDeallocation")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "node/deallocate")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("MarkVMForDeallocation Function was called");
            // Get POST body
            dynamic data = await req.Content.ReadAsAsync<object>();

            // Set name to query string or body data
            string vmName = data?.vmName;

            //trim it just in case
            vmName = vmName.Trim();

            var vm = await TableStorageHelper.Instance.GetVMByID(vmName);
            if (vm == null)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, $"VM {vmName} not found");
            }

            vm.State = VMState.MarkedForDeallocation;

            if (vm.RoomsNumber == 0)
            {
                await AzureAPIHelper.DeallocateVMAsync(vm.VMID, vm.ResourceGroup);
                vm.State = VMState.Deallocating;
            }

            //modify its state in the DB
            await TableStorageHelper.Instance.ModifyVMDetailsAsync(vm);

            return vmName == null
                ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a vmName in the request body")
                : req.CreateResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(vm));
        }
    }
}
