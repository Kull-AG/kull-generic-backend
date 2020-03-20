using System;
using System.Collections.Generic;
using System.Text;

namespace Kull.GenericBackend.Error
{
    public interface IResponseExceptionHandler
    {
        public bool CanHandle(Exception err);
        public (int statusCode, object? dataToDisplay) GetContent(Exception err);
    }
}
