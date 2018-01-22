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
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "report/gamerooms")]HttpRequestMessage req, TraceWriter log)
        {
            var payload = await req.Content.ReadAsStringAsync();
            var nodes = JsonConvert.DeserializeObject<Request>(payload);
            if (nodes.nodes == null || nodes.nodes.Length == 0)
            {
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Nodes payload not found.");
            }

            foreach (var node in nodes.nodes)
            {
                log.Info($"Reported {node.nodeId} has {node.rooms} rooms");
            }

            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Content = new StringContent(payload);
            return resp;
        }
    }
}
