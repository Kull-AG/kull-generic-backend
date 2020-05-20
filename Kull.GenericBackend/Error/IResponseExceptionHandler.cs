using System;
using System.Net.Http;

namespace Kull.GenericBackend.Error
{
    /// <summary>
    /// An interface for handling errors and return results
    /// </summary>
    public interface IResponseExceptionHandler
    {
        /// <summary>
        /// This will get invoced in case of an exception in SqlServer and allows you to handle it
        /// </summary>
        /// <param name="err">The exception object</param>
        /// <param name="formatForResponse">A formatter in case you just want to return an object that is serialized accordingly
        /// to json or xml or whatever
        /// </param>
        /// <returns>The status code and the content if the error can be handled</returns>
        public (int statusCode, HttpContent dataToDisplay)? GetContent(Exception err, Func<object, HttpContent> formatForResponse);
    }
}
