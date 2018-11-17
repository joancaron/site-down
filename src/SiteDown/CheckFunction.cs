using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

namespace SiteDown
{
    public static class CheckFunction
    {
        private const string CloudflareIpHeaderKey = "CF-Connecting-IP";

        private const string XForwadedForHeaderKey = "X-Forwarded-For";

        private static HttpClient httpClient;

        private static RetryPolicy policy;

        private static MemoryCache memoryCache;

        /// <summary>
        /// Initializes static members of the SiteDown.CheckFunction class.
        /// </summary>
        static CheckFunction()
        {
            httpClient = new HttpClient();
            policy = Policy.Handle<Exception>().WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromSeconds(0.1 * Math.Pow(2, attempt)));
            memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        [FunctionName("Check")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api")]
            HttpRequest request,
            CancellationToken cancellationToken,
            ILogger log)
        {
            IActionResult actionResult = null;

            if (request.Query.TryGetValue("url", out var values) 
                && Uri.TryCreate(values.First(), UriKind.Absolute, out var siteUrl))
            {
                string ip;

                if (request.Headers.TryGetValue(CloudflareIpHeaderKey, out var ips)
                    || request.Headers.TryGetValue(XForwadedForHeaderKey, out ips))
                {
                    ip = ips.First();
                }
                else
                {
                    ip = request.HttpContext.Connection.RemoteIpAddress.ToString();
                }

                var canExecute = !memoryCache.TryGetValue(ip, out _);

                if (canExecute)
                {
                    using (var entry = memoryCache.CreateEntry(ip))
                    {
                        entry.SlidingExpiration = TimeSpan.FromMilliseconds(500);
                    }

                    await policy.ExecuteAsync(
                        async context =>
                            {
                                var result = await httpClient.GetAsync(
                                                 siteUrl,
                                                 HttpCompletionOption.ResponseHeadersRead,
                                                 cancellationToken);
                                if (result.IsSuccessStatusCode)
                                {
                                    actionResult = new OkResult();
                                }
                            },
                        cancellationToken);
                }
                else
                {
                    actionResult = new StatusCodeResult(429);
                }
            }

            return actionResult ?? new BadRequestResult();
        }
    }
}
