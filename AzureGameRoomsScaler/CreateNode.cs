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

            var err = ValidateRequest(req, nodeParams, log);
            if (err != null)
            {
                return err;
            }

            //find out if there is any VM in MarkedForDeallocation state
            var vmsInMarkedForDeallocationState = await TableStorageHelper.Instance.GetAllVMsInStateAsync(VMState.MarkedForDeallocation);

            var vm = vmsInMarkedForDeallocationState.FirstOrDefault();
            if (vm != null)
            {
                //set it as running so as not to be deallocated when game rooms are 0
                vm.State = VMState.Running;
                await TableStorageHelper.Instance.ModifyVMDetailsAsync(vm);

                return req.CreateResponse(HttpStatusCode.OK,
                            JsonConvert.SerializeObject(vm),
                            "application/json");
            }

            var deallocatedVM = (await TableStorageHelper.Instance.GetAllVMsInStateAsync(VMState.Deallocated))
                                    .FirstOrDefault();

            if (deallocatedVM == null)
            {
                string vmName = "node" + System.Guid.NewGuid().ToString("N").Substring(0, 7);
                string sshKey = ConfigurationManager.AppSettings["VM_ADMIN_KEY"]?.ToString()
                                    ?? System.IO.File.ReadAllText(context.FunctionAppDirectory + "/default_key_rsa.pub");
                string deploymentTemplate = System.IO.File.ReadAllText(context.FunctionAppDirectory + "/vmDeploy.json");

                await DeployNode(vmName, nodeParams, sshKey, deploymentTemplate, log);

                var details = new VMDetails(vmName, nodeParams.ResourceGroup, VMState.Creating);
                await TableStorageHelper.Instance.AddVMEntityAsync(details);

                return req.CreateResponse(HttpStatusCode.OK,
                            JsonConvert.SerializeObject(details),
                            "application/json");
            }
            else
            {
                await AzureAPIHelper.StartDeallocatedVMAsync(deallocatedVM);
                deallocatedVM.State = VMState.Creating;
                await TableStorageHelper.Instance.ModifyVMDetailsAsync(deallocatedVM);
                return req.CreateResponse(HttpStatusCode.OK,
                            JsonConvert.SerializeObject(deallocatedVM),
                            "application/json");
            }
        }

        private static async Task DeployNode(string vmName, NodeParameters nodeParams, string sshKey, string deploymentTemplate, TraceWriter log)
        {
            log.Info("Creating VM");
            var appSettings = ConfigurationManager.AppSettings;

            string vmImage = !string.IsNullOrEmpty(nodeParams.Image)
                                ? nodeParams.Image
                                : appSettings["GAMESERVER_VM_IMAGE"].ToString();

            string portRange = !string.IsNullOrEmpty(nodeParams.PortRange)
                                ? nodeParams.PortRange
                                : appSettings["GAMESERVER_PORT_RANGE"].ToString();

            var parameters = JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, object>> {
                { "vmName", new Dictionary<string, object> { { "value", vmName } } },
                { "location", new Dictionary<string, object> { { "value", nodeParams.Region } } },
                { "virtualMachineSize",  new Dictionary<string, object> { { "value", nodeParams.Size } } },
                { "adminUserName",   new Dictionary<string, object> { { "value", appSettings["VM_ADMIN_NAME"]?.ToString() ?? "default_gs_admin" } } },
                { "adminPublicKey", new Dictionary<string, object> { { "value", sshKey } } },
                { "gameServerPortRange", new Dictionary<string, object> { { "value", portRange } } },
                { "reportGameroomsUrl", new Dictionary<string, object> { { "value", appSettings["REPORT_GAMEROOMS_URL"]?.ToString() } } },
                { "vmImage", new Dictionary<string, object> { { "value", vmImage } } },
            });

            log.Info($"Creating VM: {vmName}");

            // not awaiting here intentionally, since we want to return response immediately
            var deploymentTask = AzureMgmtCredentials.Instance.Azure.Deployments
                .Define($"NodeDeployment{System.Guid.NewGuid().ToString()}")
                .WithExistingResourceGroup(nodeParams.ResourceGroup)
                .WithTemplate(deploymentTemplate)
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
        }

        private static HttpResponseMessage ValidateRequest(HttpRequestMessage req, NodeParameters nodeParams, TraceWriter log)
        {
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

            if (string.IsNullOrEmpty(nodeParams.PortRange) &&
                string.IsNullOrEmpty(ConfigurationManager.AppSettings["GAMESERVER_PORT_RANGE"]?.ToString()))
            {
                log.Error("Missing port range");
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, new System.ArgumentException("Port range for game server should be specified when deploying"));
            }

            return null;
        }
    }
}
