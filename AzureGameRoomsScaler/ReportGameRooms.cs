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

    public static class ReportGameRooms
    {
        public struct NodeRooms
        {
            public string nodeId { get; set; }
            public int rooms { get; set; }
        }
        public struct Request
        {
            public NodeRooms[] nodes { get; set; }
        }
        [FunctionName("ReportGameRooms")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post")]HttpRequestMessage req, TraceWriter log)
        {
            var payload = await req.Content.ReadAsStringAsync();
            var nodes = JsonConvert.DeserializeObject<Request>(payload);
            if (nodes.nodes == null || nodes.nodes.Length == 0)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Nodes payload not found.");
            }

            List<string> deallocatedVMs = new List<string>();

            foreach (var node in nodes.nodes)
            {
                log.Info($"Reported {node.nodeId} has {node.rooms} rooms");
                if(node.rooms == 0)
                {
                    //get the state of the corresponding VM
                    var vm = await TableStorageHelper.Instance.GetVMByID(node.nodeId.Trim());
                    if(vm.State == VMState.MarkedForDeallocation)
                    {
                        deallocatedVMs.Add(vm.VMID);

                        //VM has zero rooms and marked for deallocation
                        //it's fate is sealed, bye bye! :)
                        await AzureAPIHelper.DeallocateVMAsync(vm.VMID, vm.ResourceGroup);

                        //mark it with the deallocating state in table storage
                        //VMMonitor Function will be called when it is finally deallocated
                        vm.State = VMState.Deallocating;
                        await TableStorageHelper.Instance.ModifyVMDetailsAsync(vm);
                    }
                }
            }

            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Content = new StringContent("Deallocated VMs " + JsonConvert.SerializeObject(deallocatedVMs));
            return resp;
        }

        
    }
}
