// ------------------------------------------------------------------------------------
// UpstreamAppInfoTracingMiddlewareTests.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ratings.Tests;

public class UpstreamAppInfoTracingMiddlewareTests
{
    [Fact]
    public async Task Middleware_SetsActivityTags_WhenHeadersPresent()
    {
        using var server = CreateServer();
        var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("x-app-source", "frontend");
        request.Headers.Add("x-app-version", "1.2.3");

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("frontend", json);
        Assert.Contains("1.2.3", json);
    }

    [Fact]
    public async Task Middleware_Skips_WhenActivityIsNull()
    {
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(s => s.AddRouting())
            .Configure(app =>
            {
                app.UseRouting();
                app.UseMiddleware<UpstreamAppInfoTracingMiddleware>();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.Map("/", async context =>
                    {
                        var result = new { msg = "ok" };
                        await context.Response.WriteAsJsonAsync(result);
                    });
                });
            }));

        var client = server.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("x-app-source", "web");
        request.Headers.Add("x-app-version", "1.0.0");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("ok", content);
    }

    [Fact]
    public async Task Middleware_DoesNotSetTags_WhenHeadersAreWhitespace()
    {
        using var server = CreateServer();
        var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("x-app-source", " ");
        request.Headers.Add("x-app-version", "");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("upstream.app.source", content);
        Assert.DoesNotContain("upstream.app.version", content);
    }

    [Fact]
    public async Task Middleware_DoesNotSetTags_WhenHeadersAreMissing()
    {
        using var server = CreateServer();
        var client = server.CreateClient();

        var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("upstream.app.source", content);
        Assert.DoesNotContain("upstream.app.version", content);
    }

    [Fact]
    public async Task Middleware_SetsOnlySourceTag_WhenOnlySourceHeaderPresent()
    {
        using var server = CreateServer();
        var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("x-app-source", "partial-app");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("partial-app", content);
        Assert.DoesNotContain("1.2.3", content);
    }

    [Fact]
    public async Task Middleware_SetsOnlyVersionTag_WhenOnlyVersionHeaderPresent()
    {
        using var server = CreateServer();
        var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("x-app-version", "9.9.9");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("9.9.9", content);
        Assert.DoesNotContain("frontend", content);
    }

    private static TestServer CreateServer()
    {
        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddHttpContextAccessor();
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();

                app.Use(async (context, next) =>
                {
                    var activity = new Activity("TestRequest");
                    activity.Start();
                    await next();
                    activity.Stop();
                });

                app.UseMiddleware<UpstreamAppInfoTracingMiddleware>();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.Map("/", async context =>
                    {
                        var activity = Activity.Current;
                        var result = new
                        {
                            source = activity?.GetTagItem("upstream.app.source"),
                            version = activity?.GetTagItem("upstream.app.version")
                        };
                        await context.Response.WriteAsJsonAsync(result);
                    });
                });
            }));
    }
}