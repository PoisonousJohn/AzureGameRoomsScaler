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
    public static class GetAvailableVMs
    {
        [FunctionName("GetAvailableVMs")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "node/getavailable")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("GetAvailableVMs Function was called");
            var vms = await TableStorageHelper.Instance.GetAllVMsInStateAsync(VMState.Running);

            return req.CreateResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(vms));
        }
    }
}
