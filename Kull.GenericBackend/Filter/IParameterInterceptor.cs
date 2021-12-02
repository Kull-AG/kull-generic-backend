using System.Collections.Generic;

namespace Kull.GenericBackend.Filter;

public interface IParameterInterceptor
{
    void Intercept(ICollection<Parameters.WebApiParameter> apiParams, ParameterInterceptorContext context);
}
