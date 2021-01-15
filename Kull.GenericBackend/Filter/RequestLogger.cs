using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Kull.GenericBackend.Filter
{
    public class RequestLogger
    {
        public virtual void OnRequestStart(DbCommand cmd) { }

        public virtual void OnRequestEnd(DbCommand cmd, DateTime startedAtUtc) { }
    }
}
