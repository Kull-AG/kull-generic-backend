using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;
#if NET48
using HttpContext = System.Web.HttpContextBase;
using System.Web.Routing;
using Kull.MvcCompat;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
#endif

namespace Kull.GenericBackend.Filter;

public class RequestLogger
{
    [Obsolete("Use OnRequestStartAsync instead")]
    public virtual void OnRequestStart(HttpContext context, RequestStartInfo info) { }
    public virtual Task OnRequestStartAsync(HttpContext context, RequestStartInfo info)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        OnRequestStart(context, info);
#pragma warning restore CS0618 // Type or member is obsolete
        return Task.CompletedTask;
    }


    public record RequestStartInfo(DbCommand Command);
    public record RequestEndInfo(DbCommand Command, DateTime StartedAtUtc, Exception? Error);

    [Obsolete("Use OnRequestEndAsync instead")]
    public virtual void OnRequestEnd(HttpContext context, RequestEndInfo info) { }

    public virtual Task OnRequestEndAsync(HttpContext context, RequestEndInfo info)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        OnRequestEnd(context, info);
#pragma warning restore CS0618 // Type or member is obsolete
        return Task.CompletedTask;
    }

    public enum RequestValidationFailedReason { AuthenticationNotGiven = 1, PolicyFailedNetFx = 2 }
    public record RequestValidationFailedInfo(RequestValidationFailedReason Reason);
    public virtual void OnRequestValidationFailed(HttpContext context, RequestValidationFailedInfo info) { }
}
