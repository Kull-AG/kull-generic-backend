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
        public virtual void OnRequestStart(HttpContext context, DbCommand cmd) { }

        public virtual void OnRequestEnd(HttpContext context, DbCommand cmd, DateTime startedAtUtc) { }
    }
}
