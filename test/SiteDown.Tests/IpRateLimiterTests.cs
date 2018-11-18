using System;
using System.Net;
using System.Threading;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

using Moq;

using Xunit;

namespace SiteDown.Tests
{
    public class IpRateLimiterTests
    {
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMilliseconds(500);

        private readonly Mock<HttpRequest> _requestMock;

        private readonly MemoryCache _memoryCache;

        public IpRateLimiterTests()
        {
            var ip = new StringValues("1.1.1.1");
            _requestMock = new Mock<HttpRequest>();
            _requestMock.Setup(request => request.Headers.TryGetValue(It.IsAny<string>(), out ip)).Returns(true);
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        [Fact]
        public void ShouldBlockRegisteredIps()
        {
            IpRateLimiter.CanExecute(_requestMock.Object, _memoryCache);
            var canExecute = IpRateLimiter.CanExecute(_requestMock.Object, _memoryCache);
            Assert.False(canExecute);
        }

        [Fact]
        public void ShouldNotBlockNewIps()
        {
            Assert.True(IpRateLimiter.CanExecute(_requestMock.Object, _memoryCache));
        }

        [Fact]
        public void ShouldNotBlockRegisteredIpsAfterCacheExpiration()
        {
            IpRateLimiter.CanExecute(_requestMock.Object, _memoryCache);
            Thread.Sleep(_defaultCacheDuration);
            Assert.True(IpRateLimiter.CanExecute(_requestMock.Object, _memoryCache));
        }

        [Fact]
        public void ShouldTakeRemoteIpWhenNoHeader()
        {
            var ip = new StringValues("1.1.1.1");
            var expectedIp = IPAddress.Parse("2.2.2.2");

            _requestMock.Setup(request => request.Headers.TryGetValue(It.IsAny<string>(), out ip)).Returns(false);
            _requestMock.Setup(request => request.HttpContext.Connection.RemoteIpAddress).Returns(expectedIp);

            var canExecute = IpRateLimiter.CanExecute(_requestMock.Object, _memoryCache);

            Assert.True(canExecute);
            Assert.True(_memoryCache.TryGetValue(expectedIp.ToString(), out _));
        }
    }
}
