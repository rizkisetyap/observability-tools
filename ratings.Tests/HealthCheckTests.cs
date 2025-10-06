// ------------------------------------------------------------------------------------
// HealthCheckTests.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ratings.Tests;

public class HealthCheckTests
{
    [Fact]
    public async Task HealthReady_ReturnsReady_WhenHealthy()
    {
        using var server = CreateServerHealthy();
        var client = server.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("\"status\": \"ready\"", content);
        Assert.Contains("\"sqlite\": \"up\"", content);
        Assert.Contains("\"self\": \"up\"", content);
    }

    [Fact]
    public async Task HealthReady_ReturnsNotReady_WhenUnhealthy()
    {
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddHealthChecks()
                    .AddCheck("broken", () => HealthCheckResult.Unhealthy(), new[] { "ready" });
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseHealthCheck();
            }));

        var client = server.CreateClient();
        var response = await client.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("\"status\": \"not-ready\"", content);
        Assert.Contains("\"broken\": \"down\"", content);
    }

    [Fact]
    public async Task HealthLive_ReturnsLive_WhenHealthy()
    {
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddHealthChecks()
                    .AddCheck("self", () => HealthCheckResult.Healthy(), new[] { "live" });
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseHealthCheck();
            }));

        var client = server.CreateClient();
        var response = await client.GetAsync("/health/live");
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("\"status\": \"ready\"", content);
        Assert.Contains("\"self\": \"up\"", content);
    }

    [Fact]
    public async Task UiResponseWriter_WritesMinimalResponse_Unhealthy()
    {
        var context = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                {
                    "test", new HealthReportEntry(
                        HealthStatus.Unhealthy,
                        "desc",
                        TimeSpan.FromSeconds(1),
                        null,
                        null)
                }
            },
            TimeSpan.FromSeconds(1));

        await UiResponseWriter.WriteMinimalResponse(context, report);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.Contains("\"status\": \"not-ready\"", body);
        Assert.Contains("\"test\": \"down\"", body);
        Assert.Equal(500, context.Response.StatusCode);
    }

    [Fact]
    public void TimeSpanConverter_WritesExpectedString()
    {
        var obj = new { duration = TimeSpan.FromMinutes(90) };
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TimeSpanConverter());

        var json = JsonSerializer.Serialize(obj, options);

        Assert.Contains("\"01:30:00\"", json);
    }

    [Fact]
    public void TimeSpanConverter_Read_ReturnsDefault()
    {
        var json = "\"01:30:00\"";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TimeSpanConverter());

        var value = JsonSerializer.Deserialize<TimeSpan>(json, options);

        Assert.Equal(default, value); // karena Read() return default
    }

    private static TestServer CreateServerHealthy()
    {
        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddHealthCheck();
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseHealthCheck();
            }));
    }
}
