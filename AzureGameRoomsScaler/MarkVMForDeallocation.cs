using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace AzureGameRoomsScaler
{
    public static class MarkVMForDeallocation
    {

        [FunctionName("MarkVMForDeallocation")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "node/deallocate")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("MarkVMForDeallocation Function was called");
            // Get POST body
            dynamic data = await req.Content.ReadAsAsync<object>();

            // Set name to query string or body data
            string vmName = data?.vmName;

            //trim it just in case
            vmName = vmName.Trim();

            if (await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(vmName, VMState.MarkedForDeallocation)) == VMDetailsUpdateResult.VMNotFound)
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, $"VM with name {vmName} not found");

            return vmName == null
                ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a vmName in the request body")
                : req.CreateResponse(HttpStatusCode.OK, $"VM with name {vmName} was successfully marked for deallocation");
        }
    }
}
