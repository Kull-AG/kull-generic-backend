using Ganss.Xss;
using Kull.GenericBackend.Filter;
using Kull.GenericBackend.Parameters;

namespace Kull.GenericBackend.Sanitizer;
public class SanitizerParameterInterceptor : IParameterInterceptor
{
    private readonly HtmlSanitizer sanitizer;

    public SanitizerParameterInterceptor(HtmlSanitizer sanitizer)
    {
        this.sanitizer = sanitizer;
    }

    public void Intercept(ICollection<WebApiParameter> apiParams, ParameterInterceptorContext context)
    {
        var sanitizeParameters = context.Method.GetAdditionalConfigValue<IReadOnlyCollection<string>?>("SanitizeHtmlParameters");
        if (sanitizeParameters != null && sanitizeParameters.Any())
        {
            var oldPrms = apiParams.Where(s => s.WebApiName != null && sanitizeParameters.Contains(s.WebApiName, StringComparer.OrdinalIgnoreCase)).ToArray();
            foreach (var snp in sanitizeParameters)
            {
                var oldp = apiParams.FirstOrDefault(s => s.WebApiName != null && s.WebApiName.Equals(snp, StringComparison.OrdinalIgnoreCase));
                if (oldp == null)
                {
                    throw new InvalidDataException("Html Sanitizer parameter is not found"); // Throw, could be a typo in which case we would not sanitize
                }

                apiParams.Remove(oldp);
                SanitizeParameter sanitizedhtml = new SanitizeParameter(oldp.SqlName, oldp.WebApiName, oldp.GetSchema(), this.sanitizer);
                apiParams.Add(sanitizedhtml);
            }
        }
    }
}
