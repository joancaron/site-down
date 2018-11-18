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

        private static readonly HttpClient HttpClient;

        private static readonly MemoryCache MemoryCache;

        private static readonly RetryPolicy RetryPolicy;

        /// <summary>
        /// Initializes static members of the SiteDown.CheckFunction class.
        /// </summary>
        static CheckFunction()
        {
            HttpClient = new HttpClient();
            RetryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromSeconds(0.1 * Math.Pow(2, attempt)));
            MemoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        [FunctionName("Check")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api")]
            HttpRequest request,
            CancellationToken cancellationToken,
            ILogger log)
        {
            IActionResult actionResult = null;

            if (request.Query.TryGetValue("url", out var values) && Uri.TryCreate(
                    values.First(),
                    UriKind.Absolute,
                    out var siteUrl))
            {
                var canExecute = IpRateLimiter.CanExecute(request, MemoryCache);

                if (canExecute)
                {
                    await RetryPolicy.ExecuteAsync(
                        async context =>
                        {
                            var result = await HttpClient.GetAsync(
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
