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

        private const string VM_NOT_FOUND_MESSAGE = "VM NOT FOUND: ";

        private const string VM_OPERATION = "Microsoft.Compute/virtualMachines/";
        private const string CREATE_VM_OPERATION = "Microsoft.Compute/virtualMachines/write";
        private const string RESTART_VM_OPERATION = "Microsoft.Compute/virtualMachines/restart/action";
        private const string DEALLOCATE_VM_OPERATION = "Microsoft.Compute/virtualMachines/deallocate/action";
        private const string START_VM_OPERATION = "Microsoft.Compute/virtualMachines/start/action";
        private const string OPERATION_SUCCEEDED = "Succeeded";
        private const string OPERATION_STARTED = "Started";

        [FunctionName("VMMonitor")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "VMMonitor")]HttpRequestMessage req, TraceWriter log)
        {
            dynamic dataobject = await req.Content.ReadAsAsync<object>();
            //log.Info(dataobject.ToString());

            log.Info("----------------------------------------------");

            //get Azure Monitor detailed activity log
            var activityLog = dataobject.data.context.activityLog;
            log.Info(activityLog.ToString());

            //confirm that this is indeed a VM operation
            if (activityLog.operationName.ToString().StartsWith(VM_OPERATION))
            {
                string resourceGroup = activityLog.resourceGroupName;//get the resource group
                //get the VM ID
                string resourceId = activityLog.resourceId.ToString(); // returns /subscriptions/6bd0e514-c783-4dac-92d2-6788744eee7a/resourceGroups/lala3/providers/Microsoft.Compute/virtualMachines/lala3
                string VMID = resourceId.Substring(resourceId.LastIndexOf('/') + 1);

                var vm = await TableStorageHelper.Instance.GetVMByID(VMID);
                if (vm == null)
                {
                    log.Info($"VM {VMID} was not found in our DB, skipping");
                    return req.CreateResponse(HttpStatusCode.OK);
                }

                bool operationSucceeded = activityLog.status == OPERATION_SUCCEEDED;
                string operationName = (string)activityLog.operationName;

                if (!operationSucceeded)
                {
                    vm.State = VMState.Failed;
                    log.Error($"VM {VMID} is failed: {activityLog.subStatus}");
                    await TableStorageHelper.Instance.ModifyVMDetailsAsync(vm);
                    return req.CreateResponse(HttpStatusCode.InternalServerError);
                }

                string ip;
                switch (operationName)
                {
                    case CREATE_VM_OPERATION:
                        log.Info($"VM with name {VMID} created");
                        //when the VM is finally created we need to i)set its state as Running and ii)get its Public IP
                        ip = await AzureAPIHelper.GetVMPublicIP(VMID, resourceGroup);
                        await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(VMID, resourceGroup, VMState.Running, ip));
                        break;
                    case RESTART_VM_OPERATION:
                        log.Info($"VM with name {VMID} rebooted");
                        await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(VMID, resourceGroup, VMState.Running));
                        break;
                    case DEALLOCATE_VM_OPERATION:
                        log.Info($"VM with name {VMID} deallocated");
                        //when the VM is deallocated its public IP is removed, too
                        await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(VMID, resourceGroup, VMState.Deallocated, string.Empty));
                        break;
                    case START_VM_OPERATION:
                        log.Info($"VM with name {VMID} started - was deallocated before");
                        //when the VM is started from deallocation it gets a new public IP, so add it to the DB
                        ip = await AzureAPIHelper.GetVMPublicIP(VMID, resourceGroup);
                        await TableStorageHelper.Instance.ModifyVMDetailsAsync(new VMDetails(VMID, resourceGroup, VMState.Running, ip));
                        break;

                }

                log.Info("----------------------------------------------");
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
