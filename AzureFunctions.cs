using System.Net;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Quantmatix.Robots.Settings;
using Quantmatix.Shared;
using Quantmatix.Shared.DAO;
using Quantmatix.Shared.Diagnostic;

namespace Quantmatix.Robots; 

[PublicAPI]
public class AzureFunctions(AppSettings appSettings, QMLogger logger, StudyDBUnit studyDBUnit, WebsiteDBUnit websiteDBUnit, SharedDI sharedDI) {
    [Function("Status")]
    public IActionResult Status([HttpTrigger(AuthorizationLevel.Anonymous, WebRequestMethods.Http.Get)] HttpRequest req) {
        return new OkObjectResult(new { result = "running" });
    }
}