// Copyright (c) Joan Caron and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace SiteDown
{
    public static class CheckFunction
    {
        [FunctionName("Check")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api")]
            HttpRequest req,
            string url,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            return new OkResult();
        }
    }
}
