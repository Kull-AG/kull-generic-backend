using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Kull.GenericBackend.Error
{
    public interface IResponseExceptionHandler
    {
        public (int statusCode, HttpContent dataToDisplay)? GetContent(Exception err);
    }
}
