using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kull.GenericBackend.Parameters
{
    public class FileDescriptionParameter : WebApiParameter
    {
        public override bool RequiresFormData => true;

        public FileDescriptionParameter(string webApiName): base(null, webApiName)
        {

        }

        public override OpenApiSchema GetSchema()
        {
            return new OpenApiSchema()
            {
                Type = "file",
                Format = "binary"
            };
        }

        public override object? GetValue(HttpContext http, object? valueProvided)
        {
            return null;
        }
    }
}
