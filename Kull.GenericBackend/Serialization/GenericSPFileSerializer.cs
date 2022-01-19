using Kull.Data;

#if NET48
using HttpContext = System.Web.HttpContextBase;
using Kull.MvcCompat;
using System.Net.Http.Headers;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using Kull.GenericBackend.Common;
using Kull.GenericBackend.SwaggerGeneration;
using Kull.GenericBackend.Error;

namespace Kull.GenericBackend.Serialization;

/// <summary>
/// Helper class for writing the result of a command to the body of the response
/// </summary>
public class GenericSPFileSerializer : IGenericSPSerializer
{
    public const string DefaultContentType = "application/octet-stream";

    protected string ContentColumn { get; } = "Content";
    protected string ContentTypeColumn { get; } = "ContentType";
    protected string FileNameColumn { get; } = "FileName";

    public bool SupportsResultType(string resultType) => resultType == "file";
    public int? GetSerializerPriority(IEnumerable<MediaTypeHeaderValue> contentTypes,
        Entity entity,
        Method method)
    {
        // Do not return null, Json is default/fallback
        return contentTypes.Any(contentType => contentType.MediaType == "application/octet-stream") ? 40 : 1001;
    }


    private readonly ILogger<GenericSPFileSerializer> logger;
    private readonly JsonErrorHandler jsonErrorHandler;

    public GenericSPFileSerializer(
            ILogger<GenericSPFileSerializer> logger,
            Error.JsonErrorHandler jsonErrorHandler)
    {
        this.logger = logger;
        this.jsonErrorHandler = jsonErrorHandler;
    }

    /// <summary>
    /// Prepares the header
    /// </summary>
    /// <param name="context">The http context</param>
    /// <param name="method">The Http/SP mapping</param>
    /// <param name="ent">The Entity mapping</param>
    /// <returns></returns>
    protected Task PrepareHeader(SerializationContext context, Method method, Entity ent, int statusCode, string? contentType, string? fileName)
    {
        string? contentDist = null;
        if (statusCode == 200)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                contentDist = "inline";
            }
            else
            {
                contentDist = GetContentAttachmentDisposition(context, fileName!, context.GetRequestHeader("User-Agent"));
            }
        }
        context.SetHeaders(contentType ?? DefaultContentType, statusCode, true, new Dictionary<string, string?>()
            {
                { "Content-Disposition", contentDist }
            });
        return Task.CompletedTask;
    }

    protected string GetContentAttachmentDisposition(SerializationContext context, string fileName, string? userAgent)
    {
        // Thanks, https://stackoverflow.com/questions/93551/how-to-encode-the-filename-parameter-of-content-disposition-header-in-http
        string contentDisposition;
        if (userAgent != null && userAgent.ToLowerInvariant().Contains("android")) // android built-in download manager (all browsers on android)
            contentDisposition = "attachment; filename=\"" + MakeAndroidSafeFileName(fileName) + "\"";
        else
            contentDisposition = "attachment; filename=\"" + fileName + "\"; filename*=UTF-8''" + Uri.EscapeDataString(fileName);
        return contentDisposition;
    }

    private static readonly Dictionary<char, char> AndroidAllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ._-+,@£$€!½§~'=()[]{}0123456789".ToDictionary(c => c);
    private string MakeAndroidSafeFileName(string fileName)
    {
        char[] newFileName = fileName.ToCharArray();
        for (int i = 0; i < newFileName.Length; i++)
        {
            if (!AndroidAllowedChars.ContainsKey(newFileName[i]))
                newFileName[i] = '_';
        }
        return new string(newFileName);
    }

    /// <summary>
    /// Writes the result data to the body
    /// </summary>
    /// <param name="context">The HttpContext</param>
    /// <param name="cmd">The Db Command</param>
    /// <param name="method">The Http/SP mapping</param>
    /// <param name="ent">The Entity mapping</param>
    /// <returns>A Task</returns>
    public virtual async Task<Exception?> ReadResultToBody(SerializationContext serializationContext)
    {
        var method = serializationContext.Method;
        var ent = serializationContext.Entity;
        try
        {
            using (var rdr = await serializationContext.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess | System.Data.CommandBehavior.SingleResult | System.Data.CommandBehavior.SingleRow))
            {
                bool firstRead = rdr.Read();
                if (!firstRead)
                {
                    await PrepareHeader(serializationContext, method, ent, 404, "application/json", null);
                    return null;
                }
                var fieldNames = Enumerable.Range(0, rdr.FieldCount).Select(s => rdr.GetName(s)).ToArray();
                int? fileNameOrdinal = fieldNames.Contains(FileNameColumn, StringComparer.OrdinalIgnoreCase) ? rdr.GetOrdinal(FileNameColumn): null;
                int contentTypeOrdinal = rdr.GetOrdinal(ContentTypeColumn);
                int contentColOrdinal = rdr.GetOrdinal(ContentColumn);
                string? firstValue = fileNameOrdinal == null ? rdr.GetNString(contentTypeOrdinal) : rdr.GetNString(Math.Min(fileNameOrdinal.Value, contentTypeOrdinal));
                string? secondValue = fileNameOrdinal == null  ? null: rdr.GetNString(Math.Max(fileNameOrdinal.Value, contentTypeOrdinal));
                if (Math.Max(contentColOrdinal, fileNameOrdinal == null? contentTypeOrdinal : Math.Max(fileNameOrdinal.Value, contentTypeOrdinal)) != contentColOrdinal)
                {
                    await PrepareHeader(serializationContext, method, ent, 500, "text/plain", null);
                    await serializationContext.HttpContentToResponse(new System.Net.Http.StringContent($"{ContentColumn} must come after Filename and content type"));
                    return new Exception("Error because of order");
                }
                string? fileName = fileNameOrdinal == null ? null: fileNameOrdinal.Value > contentTypeOrdinal ? secondValue : firstValue;
                string? contentType = (fileNameOrdinal == null || fileNameOrdinal.Value > contentTypeOrdinal ? firstValue : secondValue);


                await PrepareHeader(serializationContext, method, ent, 200, contentType, fileName);
                await SendContentToBody(serializationContext, rdr, contentColOrdinal);

            }
            return null;
        }
        catch (Exception err)
        {
            var handled = await jsonErrorHandler.SerializeErrorAsJson(err, serializationContext);
            if (!handled)
                throw;
            return err;
        }
    }

    protected async Task SendContentToBody(SerializationContext context, System.Data.Common.DbDataReader rdr, int contentColumnOrdinal)
    {
        //byte[] content = (byte[])rdr.GetValue(rdr.GetOrdinal(ContentColumn));

        long offset = 0;
        byte[] buffer = new byte[8 * 1024];//8K
        int bytesRead;
        do
        {
            bytesRead = (int)rdr.GetBytes(contentColumnOrdinal, offset, buffer, 0, buffer.Length);
            await context.OutputStream.WriteAsync(buffer, 0, bytesRead);
            await context.OutputStream.FlushAsync();

            offset += bytesRead;
        }
        while (bytesRead == buffer.Length);//has more bytes to read
        await context.FlushResponseAsync();
    }

    public OpenApiResponses GetResponseType(OperationResponseContext context)
    {
        // TODO: Implement Output Parameters
        var responses = new OpenApiResponses();
        responses.Add("200", new OpenApiResponse()
        {
            Description = "A binary file",
            Content = new Dictionary<string, OpenApiMediaType>(){
                        {
                            DefaultContentType,
                            new OpenApiMediaType()
                            {
                                Schema = new OpenApiSchema()
                                {
                                    // Actually in v3, type string would be correct, but I don't think this describes it correctly
                                    // https://swagger.io/docs/specification/describing-responses/
                                    Type = "file",
                                    Format = "binary"
                                }
                            }
                        }
                    }
        });
        return responses;
    }

}
