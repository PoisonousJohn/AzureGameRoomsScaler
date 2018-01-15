using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace AzureGameRoomsScaler
{
    public struct NodeParameters
    {
        // required
        public string Region { get; set; }
        // optional
        public string Image { get; set; }
        // required
        public string Size { get; set; }
    }
    public static class CreateNode
    {
        [FunctionName("Function1")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "node/create")]HttpRequestMessage req, TraceWriter log)
        {
            var content = await req.Content.ReadAsStringAsync();
            var nodeParams = JsonConvert.DeserializeObject<NodeParameters>(content);

            if (string.IsNullOrEmpty(nodeParams.Size) || string.IsNullOrEmpty(nodeParams.Region))
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, new System.ArgumentException("Node parameters <region, size> are required"));
            }

            AzureMgmtCredentials.instance.Azure.Deployments.Define("asdf").WithExistingResourceGroup("asdf").WithTemplate()
            //AzureMgmtCredentials.instance.Azure.VirtualMachines.CreateAsync()

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
