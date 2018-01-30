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

        private static string VMMONITOR_VERBOSE = "VMMONITOR_VERBOSE";
        private static string VM_NOT_FOUND_MESSAGE = "VM NOT FOUND: ";

        private static string VM_OPERATION = "Microsoft.Compute/virtualMachines/";
        private static string CREATE_VM_OPERATION = "Microsoft.Compute/virtualMachines/write";
        private static string RESTART_VM_OPERATION = "Microsoft.Compute/virtualMachines/restart/action";
        private static string DEALLOCATE_VM_OPERATION = "Microsoft.Compute/virtualMachines/deallocate/action";
        private static string START_VM_OPERATION = "Microsoft.Compute/virtualMachines/start/action";
        private static string OPERATION_SUCCEEDED = "Succeeded";
        private static string OPERATION_STARTED = "Started";

        [FunctionName("VMMonitor")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "VMMonitor")]HttpRequestMessage req, TraceWriter log)
        {
            dynamic dataobject = await req.Content.ReadAsAsync<object>();
            //log.Info(dataobject.ToString());

            if (ConfigurationManager.AppSettings[VMMONITOR_VERBOSE] == "true") log.Info("----------------------------------------------");

            //get Azure Monitor detailed activity log
            var activityLog = dataobject.data.context.activityLog;
            if (ConfigurationManager.AppSettings[VMMONITOR_VERBOSE] == "true") log.Info(activityLog.ToString());

            //confirm that this is indeed a VM operation
            if (activityLog.operationName.ToString().StartsWith(VM_OPERATION))
            {
                string resourceGroup = activityLog.resourceGroupName;//get the resource group
                //get the VM ID
                string resourceId = activityLog.resourceId.ToString(); // returns /subscriptions/6bd0e514-c783-4dac-92d2-6788744eee7a/resourceGroups/lala3/providers/Microsoft.Compute/virtualMachines/lala3
                string VMID = resourceId.Substring(resourceId.LastIndexOf('/') + 1);

                if (activityLog.operationName == CREATE_VM_OPERATION && activityLog.status == OPERATION_SUCCEEDED)
                {
                    log.Info($"VM with name {VMID} created");
                    //when the VM is finally created we need to i)set its state as Running and ii)get its Public IP
                    string ip = await AzureAPIHelper.GetVMPublicIP(VMID, resourceGroup);
                    await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(VMID, resourceGroup, VMState.Running, ip));
                }
                else if (activityLog.operationName == RESTART_VM_OPERATION && activityLog.status == OPERATION_SUCCEEDED)
                {
                    log.Info($"VM with name {VMID} rebooted");
                    await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(VMID, resourceGroup, VMState.Running));
                }
                else if (activityLog.operationName == DEALLOCATE_VM_OPERATION && activityLog.status == OPERATION_SUCCEEDED)
                {
                    log.Info($"VM with name {VMID} deallocated");
                    //when the VM is deallocated its public IP is removed, too
                    await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(VMID, resourceGroup, VMState.Deallocated, string.Empty)); 
                }
                else if (activityLog.operationName == START_VM_OPERATION && activityLog.status == OPERATION_SUCCEEDED)
                {
                    log.Info($"VM with name {VMID} started - was deallocated before");
                    //when the VM is started from deallocation it gets a new public IP, so add it to the DB
                    string ip = await AzureAPIHelper.GetVMPublicIP(VMID, resourceGroup);
                    await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(VMID, resourceGroup, VMState.Running, ip));
                }

                if (ConfigurationManager.AppSettings[VMMONITOR_VERBOSE] == "true") log.Info("----------------------------------------------");
                return req.CreateResponse(HttpStatusCode.OK, $"WebHook call for VM:'{VMID}' with operation:'{activityLog.operationName}' and status:'{activityLog.status}' was successful");
            }
            else
            {
                string msg = "No VM operation, something went wrong";
                log.Error(msg);
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, msg);
            }
        }
    }
}
