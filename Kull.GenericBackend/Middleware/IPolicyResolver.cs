#if NET48
using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Kull.GenericBackend.Middleware;

public interface IPolicyResolver
{
    public bool UserIsInPolicy(HttpContextBase context, string policyName);
}

internal class ThrowPolicyResolver : IPolicyResolver
{
    public bool UserIsInPolicy(HttpContextBase context, string policyName)
    {
        throw new ArgumentException("Full .Net does only support policies when injecting an IPolicyResolver manually");
    }
}

#endif
