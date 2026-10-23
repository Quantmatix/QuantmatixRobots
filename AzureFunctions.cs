using System.Net;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Quantmatix.Robots.Settings;
using Quantmatix.Shared;
using Quantmatix.Shared.DAO;
using Quantmatix.Shared.Diagnostic;

namespace Quantmatix.Robots; 

[PublicAPI]
public class AzureFunctions(AppSettings appSettings, QMLogger logger, StudyDBUnit studyDBUnit, WebsiteDBUnit websiteDBUnit, SharedDI sharedDI) {
    [Function("Status")]
    public IActionResult Status([HttpTrigger(AuthorizationLevel.Anonymous, WebRequestMethods.Http.Get)] HttpRequestData requestData) {
        return new OkObjectResult(new { result = "running" });
    }
}