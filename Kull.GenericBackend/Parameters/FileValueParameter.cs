#if NET47
using HttpContext = System.Web.HttpContextBase;
using Kull.MvcCompat;
#else
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Configuration;

namespace Kull.GenericBackend.Parameters
{
    public class FileValueParameter : WebApiParameter
    {
        private readonly string fileFieldName;

        public override bool RequiresFormData => true;

        public FileValueParameter(string fileFieldName, 
                string sqlName): base(sqlName, null)
        {
            this.fileFieldName = fileFieldName;
        }

        public override OpenApiSchema GetSchema()
        {
            return null!;
        }

        private byte[] GetByteFromStream(System.IO.Stream stream)
        {
            // Thanks,  https://stackoverflow.com/questions/1080442/how-to-convert-an-stream-into-a-byte-in-c
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }

        }

        public override object? GetValue(HttpContext http, object? valueProvided)
        {
            var allPrms = (Dictionary<string, object>)valueProvided!;
#if NETFX
            var file = (System.Web.HttpPostedFileBase)allPrms[this.fileFieldName];
#else
            var file = (IFormFile)allPrms[this.fileFieldName];
#endif
            if (this.SqlName!.EndsWith("_Content", StringComparison.CurrentCultureIgnoreCase))
            {
                using var str = file.OpenReadStream();
                return GetByteFromStream(str);
            }
            if (this.SqlName!.EndsWith("_ContentType", StringComparison.CurrentCultureIgnoreCase))
            {
                return file.ContentType;
            }
            if (this.SqlName!.EndsWith("_Length", StringComparison.CurrentCultureIgnoreCase))
            {
#if NETFX
                return file.ContentLength;
#else
                return file.Length;
#endif
            }
            if (this.SqlName!.EndsWith("_FileName", StringComparison.CurrentCultureIgnoreCase))
            {
                return file.FileName;
            }
            if (this.SqlName!.EndsWith("_Headers", StringComparison.CurrentCultureIgnoreCase))
            {
                // Untested
#if NETFX
                return null;
#else
                return Newtonsoft.Json.JsonConvert.SerializeObject(file.Headers);
#endif
            }
            throw new NotSupportedException("Not supported");
        }
    }
}
