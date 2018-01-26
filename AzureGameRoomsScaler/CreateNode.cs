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
using System.Linq;

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

            //find out if there is any VM in MarkedForDeallocation state
            var vmsInMarkedForDeallocationState = await TableStorageHelper.Instance.GetAllVMsInStateAsync(VMState.MarkedForDeallocation);
            if (vmsInMarkedForDeallocationState.Count() > 0)
            {
                //get the first one
                var vm = vmsInMarkedForDeallocationState.First();
                //set it as running
                vm.State = VMState.Running;
                if (await TableStorageHelper.Instance.ModifyVMDetailsAsync(vm) == VMDetailsUpdateResult.VMNotFound)
                    throw new System.Exception($"Error updating VM with ID {vm.VMID}");

                var result = new Dictionary<string, string>
                {
                    { "nodeId", vm.VMID },
                    { "Result", "Re-using an old VM" }
                };

                return req.CreateResponse(HttpStatusCode.OK,
                            JsonConvert.SerializeObject(result),
                            "application/json");
            }

            else
            {
                string portRange = !string.IsNullOrEmpty(nodeParams.PortRange)
                                    ? nodeParams.PortRange
                                    : ConfigurationManager.AppSettings["GAMESERVER_PORT_RANGE"].ToString();


                string vmName = "node" + System.Guid.NewGuid().ToString("N").Substring(0, 7);

                var details = new VMDetails(vmName, VMState.Creating);
                await TableStorageHelper.Instance.AddVMEntityAsync(details);

                log.Info("Creating VM");
                var appSettings = ConfigurationManager.AppSettings;

                var parameters = JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, object>> {
                    { "vmName", new Dictionary<string, object> { { "value", vmName } } },
                    { "location", new Dictionary<string, object> { { "value", nodeParams.Region } } },
                    { "virtualMachineSize",  new Dictionary<string, object> { { "value", nodeParams.Size } } },
                    { "adminUserName",   new Dictionary<string, object> { { "value", appSettings["VM_ADMIN_NAME"]?.ToString() ?? "default_gs_admin" } } },
                    { "adminPublicKey", new Dictionary<string, object> { { "value", appSettings["VM_ADMIN_KEY"]?.ToString() ?? System.IO.File.ReadAllText(context.FunctionAppDirectory + "/default_key_rsa.pub") } } },
                    { "gameServerPortRange", new Dictionary<string, object> { { "value", portRange } } },
                    { "reportGameroomsUrl", new Dictionary<string, object> { { "value", appSettings["REPORT_GAMEROOMS_URL"]?.ToString() } } },
                    { "vmImage", new Dictionary<string, object> { { "value", vmImage } } },
                });

                log.Info($"Creating VM: {vmName}");

                // not awaiting here intentionally, since we want to return response immediately
                var deploymentTask = AzureMgmtCredentials.instance.Azure.Deployments
                    .Define($"NodeDeployment{System.Guid.NewGuid().ToString()}")
                    .WithExistingResourceGroup(nodeParams.ResourceGroup)
                    .WithTemplate(System.IO.File.ReadAllText(context.FunctionAppDirectory + "/vmDeploy.json"))
                    .WithParameters(parameters)
                    .WithMode(DeploymentMode.Incremental)
                    .CreateAsync();

                // when don't want to await completion of ARM deployment since it can take up to 5 mins
                // so let's give it few seconds to start and return in case we have deployment exception
                // like service principal error
                await Task.WhenAny(deploymentTask, Task.Delay(1000));
                if (deploymentTask.IsCompleted && deploymentTask.IsFaulted)
                {
                    throw deploymentTask.Exception;
                }

                var result = new Dictionary<string, string>
                {
                    { "nodeId", vmName },
                    { "Result", "Created a new VM" }
                };

                return req.CreateResponse(HttpStatusCode.OK,
                            JsonConvert.SerializeObject(result),
                            "application/json");
            }
        }
    }
}
