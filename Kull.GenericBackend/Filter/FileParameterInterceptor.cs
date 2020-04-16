using Kull.GenericBackend.SwaggerGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kull.GenericBackend.Filter
{
    public class FileParameterInterceptor : IParameterInterceptor
    {
        // Supported postfixes of Parameters.FileValueParameters
        static readonly string[] SupportedPostFixes = new string[]
            {"Content",
            "ContentType",
            "FileName",
            "Length",
            "Headers"};

        public void Intercept(ICollection<Parameters.WebApiParameter> apiParams)
        {
            var fileParameters = apiParams.Where(p =>
                (p.SqlName ?? "").Contains("_"))
                .Select(s => new
                {
                    Parameter = s,
                    FileParameterName = s.SqlName!.Substring(0, s.SqlName!.LastIndexOf('_')),
                    Postfix = s.SqlName!.Substring(s.SqlName!.LastIndexOf('_') + 1)
                })
                .GroupBy(
                    p => p.FileParameterName
                )
                // at least two file parameters, one of them beeing content and all understood
                .Where(prmGrp => prmGrp.Count() >= 2
                && prmGrp.Any(p => p.Postfix.Equals("Content", StringComparison.CurrentCultureIgnoreCase))
                && prmGrp.All(p =>
                    SupportedPostFixes.Contains(p.Postfix, StringComparer.CurrentCultureIgnoreCase)
                )).ToArray();
                    
            foreach(var fileParameter in fileParameters)
            {
                foreach(var existing in fileParameter)
                {
                    apiParams.Remove(existing.Parameter);
                    apiParams.Add(new Parameters.FileValueParameter(fileParameter.Key,
                        existing.Parameter.SqlName!));
                }
                apiParams.Add(new Parameters.FileDescriptionParameter(fileParameter.Key));
            }
        }
    }
}
