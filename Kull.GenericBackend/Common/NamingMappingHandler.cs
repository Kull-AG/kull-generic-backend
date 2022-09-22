using Kull.GenericBackend.Middleware;
using System;
using System.Collections.Generic;

namespace Kull.GenericBackend.Common;

/// <summary>
/// A class for handling the mapping between SQL Server Naming Convention and REST Naming Convention (Camel Case)
/// Handles duplicate field names as well
/// </summary>
public class NamingMappingHandler
{
    private readonly SPMiddlewareOptions options;
    internal static readonly string IgnoreFieldPlaceHolder = "________ignore";

    public NamingMappingHandler(SPMiddlewareOptions options)
    {
        this.options = options;
    }

    public IEnumerable<string> GetNames(IEnumerable<string?>? dt)
    {
        var setNames = new List<string>();
        int nullCount = 0;
        if (dt == null) yield break;
        foreach (var item in dt)
        {
#if NEWTONSOFTJSON
            string? name = item == null ? null : options.NamingStrategy.GetPropertyName(item, false);
#else
            string? name = item == null ? null : (options.NamingStrategy == null ? item : options.NamingStrategy.ConvertName(item));
#endif
            if (name == IgnoreFieldPlaceHolder)
            {
                yield return name;
                continue;
            }
            if (name == null)
            {
                name = "column" + (nullCount == 0 ? "" : nullCount.ToString());
                nullCount++;
            }
            var origName = name;
            int i = 1;
            while (setNames.Contains(name))
            {
                name = origName + "_" + i.ToString();
                i++;
            }
            setNames.Add(name);
            yield return name;
        }
    }
}
