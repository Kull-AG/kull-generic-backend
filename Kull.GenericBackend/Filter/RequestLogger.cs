using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
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

namespace Kull.GenericBackend.Filter
{
    public class RequestLogger
    {
        public virtual void OnRequestStart(HttpContext context, RequestStartInfo info) { }

        public record RequestStartInfo(DbCommand Command);
        public record RequestEndInfo(DbCommand Command, DateTime StartedAtUtc, Exception? Error);

        public virtual void OnRequestEnd(HttpContext context, RequestEndInfo info) { }

        public enum RequestValidationFailedReason { AuthenticationNotGiven=1 }
        public record RequestValidationFailedInfo(RequestValidationFailedReason Reason);
        public virtual void OnRequestValidationFailed(HttpContext context, RequestValidationFailedInfo info) { }
    }
}
