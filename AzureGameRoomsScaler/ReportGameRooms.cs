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
        [FunctionName("ReportGameRooms")]
        public static HttpResponseMessage Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "report/gamerooms")]HttpRequestMessage req, TraceWriter log)
        {
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
