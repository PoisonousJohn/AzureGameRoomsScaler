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
        [FunctionName("VMMonitor")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "VMMonitor")]HttpRequestMessage req, TraceWriter log)
        {
            // Get request body
            dynamic dataobject = await req.Content.ReadAsAsync<object>();
            //log.Info(dataobject.ToString());
            log.Info("----------------------------------------------");
            var activityLog = dataobject.data.context.activityLog;
            log.Info(activityLog.ToString());
            if (activityLog.operationName == "Microsoft.Compute/virtualMachines/write")
            {
                if (activityLog.subStatus == "")
                    log.Info("VM creating");
                else if (activityLog.subStatus == "Created")
                    log.Info("VM created");
            }
            else if (activityLog.operationName == "Microsoft.Compute/virtualMachines/restart/action" && activityLog.status == "Succeeded")
            {
                log.Info("VM rebooted");
            }
            else if (activityLog.operationName == "Microsoft.Compute/virtualMachines/deallocate/action" && activityLog.status == "Succeeded")
            {
                log.Info("VM deallocated");
            }
            else if (activityLog.operationName == "Microsoft.Compute/virtualMachines/start/action" && activityLog.status == "Succeeded")
            {
                log.Info("VM started");
            }
            log.Info("----------------------------------------------");
            return req.CreateResponse(HttpStatusCode.OK, "Hello");
        }
    }
}
