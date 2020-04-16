using System;
using System.Collections.Generic;
using System.Text;
using Kull.GenericBackend.SwaggerGeneration;

namespace Kull.GenericBackend.Filter
{
    public interface IParameterInterceptor
    {
        void Intercept(ICollection<Parameters.WebApiParameter> apiParams);
    }
}
