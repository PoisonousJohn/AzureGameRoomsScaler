using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
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
        public string ResourceGroup { get; set; }
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

            log.Info("Creating VM");

            var parameters = JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, object>> {
                    { "location", new Dictionary<string, object> { { "value", nodeParams.Region } } },
                    { "virtualMachineSize",  new Dictionary<string, object> { { "value", nodeParams.Size } } },
                    { "adminUserName",   new Dictionary<string, object> { { "value", ConfigurationManager.AppSettings["VM_ADMIN_NAME"]?.ToString() ?? "default_gs_admin" } } },
                    { "adminPublicKey", new Dictionary<string, object> { { "value", ConfigurationManager.AppSettings["VM_ADMIN_KEY"]?.ToString() ?? System.IO.File.ReadAllText("default_key_rsa.pub") } } },
                    { "gameServerPortRange", new Dictionary<string, object> { { "value", ConfigurationManager.AppSettings["GAME_SERVER_PORT_RANGE"]?.ToString() ?? "25565" } } },
                    { "vmImage", new Dictionary<string, object> { { "value" ,nodeParams.Image } } },
                });

            var deployment = await AzureMgmtCredentials.instance.Azure.Deployments
                .Define($"NodeDeployment{System.Guid.NewGuid().ToString()}")
                .WithExistingResourceGroup(nodeParams.ResourceGroup)
                .WithTemplate(System.IO.File.ReadAllText("vmDeploy.json"))
                .WithParameters(parameters)
                .WithMode(DeploymentMode.Incremental)
                .CreateAsync();

            log.Info($"VM created: { JsonConvert.SerializeObject(deployment.Outputs)}");


            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
