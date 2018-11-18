using System;
using System.Linq;

using EnsureThat;

using JetBrains.Annotations;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SiteDown
{
    public static class IpRateLimiter
    {
        private const string CloudflareIpHeaderKey = "CF-Connecting-IP";

        private const string XForwadedForHeaderKey = "X-Forwarded-For";

        /// <summary>
        /// Determine if we can execute asynchronously the action.
        /// </summary>
        /// <param name="request">The current request. This cannot be null.</param>
        /// <returns>
        /// True if we can execute the action, false if not.
        /// </returns>
        public static bool CanExecute([NotNull] HttpRequest request, [NotNull] IMemoryCache memoryCache)
        {
            EnsureArg.IsNotNull(request);

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
            }

            return canExecute;
        }
    }
}
