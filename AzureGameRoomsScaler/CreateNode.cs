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
        #region Required params

        public string Region { get; set; }
        public string Size { get; set; }
        public string ResourceGroup { get; set; }

        #endregion

        #region Optional params

        /// <summary>
        /// Optional param. If ommitted AppSettings["GAMESERVER_VM_IMAGE"] will be used
        /// </summary>
        public string Image { get; set; }
        /// <summary>
        /// Optional param. If ommitted AppSettings["GAMESERVER_PORT_RANGE"] will be used
        /// </summary>
        public string PortRange { get; set; }

        #endregion
    }
    public static class CreateNode
    {
        [FunctionName("CreateNode")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "node/create")]HttpRequestMessage req, TraceWriter log, ExecutionContext context)
        {
            var content = await req.Content.ReadAsStringAsync();
            var nodeParams = JsonConvert.DeserializeObject<NodeParameters>(content);

            if (string.IsNullOrEmpty(nodeParams.Size) || string.IsNullOrEmpty(nodeParams.Region))
            {
                log.Error("Missing size or region params");
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, new System.ArgumentException("Node parameters <region, size> are required"));
            }

            if (string.IsNullOrEmpty(nodeParams.Image) &&
                string.IsNullOrEmpty(ConfigurationManager.AppSettings["GAMESERVER_VM_IMAGE"]?.ToString()))
            {
                log.Error("Missing image param");
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, new System.ArgumentException("Image for vm should be specified when deploying"));
            }

            string vmImage = !string.IsNullOrEmpty(nodeParams.Image)
                                ? nodeParams.Image
                                : ConfigurationManager.AppSettings["GAMESERVER_VM_IMAGE"].ToString();

            if (string.IsNullOrEmpty(nodeParams.PortRange) &&
                string.IsNullOrEmpty(ConfigurationManager.AppSettings["GAMESERVER_PORT_RANGE"]?.ToString()))
            {
                log.Error("Missing port range");
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, new System.ArgumentException("Port range for game server should be specified when deploying"));
            }

            string portRange = !string.IsNullOrEmpty(nodeParams.PortRange)
                                ? nodeParams.PortRange
                                : ConfigurationManager.AppSettings["GAMESERVER_PORT_RANGE"].ToString();


            log.Info("Creating VM");
            var appSettings = ConfigurationManager.AppSettings;

            var parameters = JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, object>> {
                    { "location", new Dictionary<string, object> { { "value", nodeParams.Region } } },
                    { "virtualMachineSize",  new Dictionary<string, object> { { "value", nodeParams.Size } } },
                    { "adminUserName",   new Dictionary<string, object> { { "value", appSettings["VM_ADMIN_NAME"]?.ToString() ?? "default_gs_admin" } } },
                    { "adminPublicKey", new Dictionary<string, object> { { "value", appSettings["VM_ADMIN_KEY"]?.ToString() ?? System.IO.File.ReadAllText(context.FunctionAppDirectory + "/default_key_rsa.pub") } } },
                    { "gameServerPortRange", new Dictionary<string, object> { { "value", portRange } } },
                    { "reportGameroomsUrl", new Dictionary<string, object> { { "value", appSettings["REPORT_GAMEROOMS_URL"]?.ToString() } } },
                    { "vmImage", new Dictionary<string, object> { { "value", vmImage } } },
                });

            var deployment = await AzureMgmtCredentials.instance.Azure.Deployments
                .Define($"NodeDeployment{System.Guid.NewGuid().ToString()}")
                .WithExistingResourceGroup(nodeParams.ResourceGroup)
                .WithTemplate(System.IO.File.ReadAllText(context.FunctionAppDirectory + "/vmDeploy.json"))
                .WithParameters(parameters)
                .WithMode(DeploymentMode.Incremental)
                .CreateAsync();

            log.Info($"VM created: { JsonConvert.SerializeObject(deployment.Outputs)}");


            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
