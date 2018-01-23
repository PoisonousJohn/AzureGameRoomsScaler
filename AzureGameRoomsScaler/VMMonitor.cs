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

    public static class VMMonitor
    {

		public static string VMMONITOR_VERBOSE = "VMMONITOR_VERBOSE";

        [FunctionName("VMMonitor")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "VMMonitor")]HttpRequestMessage req, TraceWriter log)
        {
            // Get request body
            dynamic dataobject = await req.Content.ReadAsAsync<object>();
            //log.Info(dataobject.ToString());
            
			if (ConfigurationManager.AppSettings[VMMONITOR_VERBOSE] == "true") log.Info("----------------------------------------------");
            var activityLog = dataobject.data.context.activityLog;
            if (ConfigurationManager.AppSettings[VMMONITOR_VERBOSE] == "true") log.Info(activityLog.ToString());

            //confirm that this is indeed a VM operation
            if (activityLog.operationName.ToString().StartsWith("Microsoft.Compute/virtualMachines/"))
            {
				//get the VM name
				string resourceId = activityLog.resourceId.ToString(); // /subscriptions/6bd0e514-c783-4dac-92d2-6788744eee7a/resourceGroups/lala3/providers/Microsoft.Compute/virtualMachines/lala3
				string vmName = resourceId.Substring(resourceId.LastIndexOf('/') + 1);
                if (activityLog.operationName == "Microsoft.Compute/virtualMachines/write" && activityLog.status == "Started")
                {
                    log.Info($"VM with name {vmName} is being created");
                    await TableStorageHelper.Instance.AddVMDetailsAsync(vmName, VMState.Creating);
                }
                else if (activityLog.operationName == "Microsoft.Compute/virtualMachines/write" && activityLog.status == "Succeeded")
                {
                    log.Info($"VM with name {vmName} created");
                    await TableStorageHelper.Instance.ModifyVMStateAsync(vmName, VMState.Running);
                }
                else if (activityLog.operationName == "Microsoft.Compute/virtualMachines/restart/action" && activityLog.status == "Succeeded")
                {
                    log.Info($"VM with name {vmName} rebooted");
                    await TableStorageHelper.Instance.ModifyVMStateAsync(vmName, VMState.Running);
                }
                else if (activityLog.operationName == "Microsoft.Compute/virtualMachines/deallocate/action" && activityLog.status == "Succeeded")
                {
                    log.Info($"VM with name {vmName} deallocated");
                    await TableStorageHelper.Instance.ModifyVMStateAsync(vmName, VMState.Deallocated);
                }
                else if (activityLog.operationName == "Microsoft.Compute/virtualMachines/start/action" && activityLog.status == "Succeeded")
                {
                    log.Info($"VM with name {vmName} started - was deallocated before");
                    await TableStorageHelper.Instance.ModifyVMStateAsync(vmName, VMState.Running);
                }
            }
			else
			{
				string msg = "No VM operation, something went wrong";
				log.Error(msg);
				return req.CreateErrorResponse(HttpStatusCode.InternalServerError, msg);
			}
            
			if (ConfigurationManager.AppSettings[VMMONITOR_VERBOSE] == "true") log.Info("----------------------------------------------");
            
			return req.CreateResponse(HttpStatusCode.OK, "WebHook call successful");
        }
    }
}
