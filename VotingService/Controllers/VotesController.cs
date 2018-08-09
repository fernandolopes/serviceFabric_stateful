using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using VotingService;
using System.Fabric;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http;

public class VotesController : Controller
{
    // Used for health checks.
   public static long _requestCount = 0L;

    // Holds the votes and counts. NOTE: THIS IS NOT THREAD SAFE FOR THE PURPOSES OF THE LAB ONLY.
    //static Dictionary<string, int> _counts = new Dictionary<string, int>();
    
    protected HttpClient _client = new HttpClient();

    [HttpGet]
    [Route("api/votes")]
    public async Task<ActionResult> Get()
    {
        string activityId = Guid.NewGuid().ToString();
        ServiceEventSource.Current.ServiceRequestStart("VotesController.Get", activityId);

        Interlocked.Increment(ref _requestCount);

        string url = $"http://localhost:19081/Voting/VotingState/api/votes?PartitionKey=0&PartitionKind=Int64Range";
        HttpResponseMessage msg = await _client.GetAsync(url).ConfigureAwait(false);
        string json = await msg.Content.ReadAsStringAsync().ConfigureAwait(false);
        List<KeyValuePair<string, int>> votes = JsonConvert.DeserializeObject<List<KeyValuePair<string, int>>>(json);

        ServiceEventSource.Current.ServiceRequestStop("VotesController.Get", activityId);
        return Ok(votes);
    }

    [HttpPost]
    [Route("api/{key}")]
    public async Task<ActionResult> Post(string key)
    {
        string activityId = Guid.NewGuid().ToString();
        ServiceEventSource.Current.ServiceRequestStart("VotesController.Get", activityId);

        Interlocked.Increment(ref _requestCount);
        Response.ContentType = "application/json";

        string url = $"http://localhost:19081/Voting/VotingState/api/{key}?PartitionKey=0&PartitionKind=Int64Range";
        HttpResponseMessage msg = await _client.PostAsync(url, null).ConfigureAwait(false);

        ServiceEventSource.Current.ServiceRequestStop("VotesController.Get", activityId);
        return StatusCode((int)msg.StatusCode, null);
    }

    [HttpDelete]
    [Route("api/{key}")]
    public ActionResult Delete(string key)
    {
        string activityId = Guid.NewGuid().ToString();
        ServiceEventSource.Current.ServiceRequestStart("VotesController.Get", activityId);

        Interlocked.Increment(ref _requestCount);
        Response.ContentType = "application/json";

        // Ignoring delete for this lab.

        ServiceEventSource.Current.ServiceRequestStop("VotesController.Get", activityId);
        return NotFound();
    }

    [HttpGet]
    [Route("api/{file}")]
    public ActionResult GetFile(string file)
    {
        string activityId = Guid.NewGuid().ToString();
        ServiceEventSource.Current.ServiceRequestStart("VotesController.Get", activityId);

        string response = null;
        Response.ContentType = "text/html";

        Interlocked.Increment(ref _requestCount);

        // Validate file name.
        if ("index.html" == file)
        {
            // This hardcoded path is only for the lab. Later in the lab when the version is changed, this
            // hardcoded path must be changed to use the UX. In part 2 of the lab, this will be calculated
            // using the connected service path.
            //string path = string.Format(@"..\VotingServicePkg.Code.1.0.0\{0}", file);
            string path = Path.Combine(FabricRuntime.GetActivationContext().GetCodePackageObject("Code").Path, "index.html");
            response = System.IO.File.ReadAllText(path);
        }

        ServiceEventSource.Current.ServiceRequestStop("VotesController.Get", activityId);

        if (null != response)
            return Ok(response);
        else
            return NotFound("File");
    }
}
