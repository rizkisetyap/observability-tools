// ------------------------------------------------------------------------------------
// UpstreamAppInfoTracingMiddleware.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using System.Diagnostics;

namespace ratings;
/// <summary>
/// UpstreamAppInfoTracingMiddleware
/// </summary>
/// <param name="next"></param>
public class UpstreamAppInfoTracingMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        var activity = Activity.Current;

        if (activity != null)
        {
            var appSource = context.Request.Headers["x-app-source"].FirstOrDefault();
            var appVersion = context.Request.Headers["x-app-version"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(appSource))
                activity.SetTag("upstream.app.source", appSource);

            if (!string.IsNullOrWhiteSpace(appVersion))
                activity.SetTag("upstream.app.version", appVersion);
        }

        await next(context);
    }
}