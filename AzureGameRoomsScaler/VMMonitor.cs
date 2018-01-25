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
                //get the VM name
                string resourceId = activityLog.resourceId.ToString(); // returns /subscriptions/6bd0e514-c783-4dac-92d2-6788744eee7a/resourceGroups/lala3/providers/Microsoft.Compute/virtualMachines/lala3
                string vmName = resourceId.Substring(resourceId.LastIndexOf('/') + 1);
                if (activityLog.operationName == CREATE_VM_OPERATION && activityLog.status == OPERATION_STARTED)
                {
                    log.Info($"VM with name {vmName} is being created");
                    await TableStorageHelper.Instance.AddVMEntityAsync(new VMDetails(vmName, VMState.Creating));
                }
                else if (activityLog.operationName == CREATE_VM_OPERATION && activityLog.status == OPERATION_SUCCEEDED)
                {
                    log.Info($"VM with name {vmName} created");
                    if (await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(vmName, VMState.Running)) == VMDetailsUpdateResult.VMNotFound)
                        return req.CreateErrorResponse(HttpStatusCode.BadRequest, VM_NOT_FOUND_MESSAGE + vmName);
                }
                else if (activityLog.operationName == RESTART_VM_OPERATION && activityLog.status == OPERATION_SUCCEEDED)
                {
                    log.Info($"VM with name {vmName} rebooted");
                    if (await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(vmName, VMState.Running)) == VMDetailsUpdateResult.VMNotFound)
                        return req.CreateErrorResponse(HttpStatusCode.BadRequest, VM_NOT_FOUND_MESSAGE + vmName);
                }
                else if (activityLog.operationName == DEALLOCATE_VM_OPERATION && activityLog.status == OPERATION_SUCCEEDED)
                {
                    log.Info($"VM with name {vmName} deallocated");
                    if (await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(vmName, VMState.Deallocated)) == VMDetailsUpdateResult.VMNotFound)
                        return req.CreateErrorResponse(HttpStatusCode.BadRequest, VM_NOT_FOUND_MESSAGE + vmName);
                }
                else if (activityLog.operationName == START_VM_OPERATION && activityLog.status == OPERATION_SUCCEEDED)
                {
                    log.Info($"VM with name {vmName} started - was deallocated before");
                    if (await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(vmName, VMState.Running)) == VMDetailsUpdateResult.VMNotFound)
                        return req.CreateErrorResponse(HttpStatusCode.BadRequest, VM_NOT_FOUND_MESSAGE + vmName);
                }

                if (ConfigurationManager.AppSettings[VMMONITOR_VERBOSE] == "true") log.Info("----------------------------------------------");
                return req.CreateResponse(HttpStatusCode.OK, $"WebHook call for VM:'{vmName}' with operation:'{activityLog.operationName}' and status:'{activityLog.status}' was successful");
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
