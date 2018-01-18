using System.Net;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    // Get request body
    dynamic dataobject = await req.Content.ReadAsAsync<object>();
    //log.Info(dataobject.ToString());

	var activityLog = dataobject.data.context.activityLog;
    //log.Info(activityLog.ToString());
    if(activityLog.operationName == "Microsoft.Compute/virtualMachines/write" )
    {
		if(activityLog.subStatus == "")
			log.Info("VM creating");
		else if(activityLog.subStatus == "Created")
            log.Info("VM created");
    }
    else if(activityLog.operationName=="Microsoft.Compute/virtualMachines/restart/action" && activityLog.status == "Succeeded")
    {
        log.Info("VM rebooted");
    }
    else if(activityLog.operationName=="Microsoft.Compute/virtualMachines/deallocate/action" && activityLog.status == "Succeeded")
    {
        log.Info("VM deallocated");
    }
    else if(activityLog.operationName=="Microsoft.Compute/virtualMachines/start/action" && activityLog.status == "Succeeded")
    {
        log.Info("VM started");
    }

    return req.CreateResponse(HttpStatusCode.OK, "Hello");
}
