using Kull.GenericBackend.Config;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;

namespace Kull.GenericBackend.Common
{
    /// <summary>
    /// Represents an HTTP Method that is mapped to a Stored Procedure
    /// </summary>
    public class Method
    {
        /// <summary>
        /// The Http Method
        /// </summary>
        public OperationType HttpMethod { get; }


        public Data.DBObjectName SP { get; }

        public string? OperationId { get; }
        
        public string? OperationName { get; }

        public string? ResultType { get; }
        public string? Tag { get; }
        
        public int? CommandTimeout { get; }

        private IReadOnlyDictionary<string, object?> restParameters;

        public IReadOnlyDictionary<string, object?>? ExecuteParameters { get; }

        public IReadOnlyCollection<string> IgnoreParameters { get; }
        public IReadOnlyCollection<string> IgnoreFields { get; }

        public Method(OperationType httpMethod, string sp)
            : this(httpMethod, sp, null, null, null)
        {

        }

        private Method(OperationType httpMethod, string sp, 
            string? operationId = null,
            string? operationName = null,
            string? resultType = null, string? tag = null,
            int? commandTimeout = null,
            IReadOnlyDictionary<string, object?>? executeParameters=null,
            IReadOnlyCollection<string>? ignoreParameters = null,
            IReadOnlyDictionary<string, object?>? restParameters = null,
            IReadOnlyCollection<string>? ignoreFields = null)
        {
            if (sp == null) throw new ArgumentNullException("sp");
            HttpMethod = httpMethod;
            SP = sp;
            OperationId = operationId;
            OperationName = operationName;
            ResultType = resultType;
            Tag = tag;
            CommandTimeout = commandTimeout;
            this.ExecuteParameters = executeParameters;
            this.IgnoreParameters = ignoreParameters ?? Array.Empty<string>();
            this.IgnoreFields = ignoreFields ?? Array.Empty<string>();
            this.restParameters = restParameters ?? new Dictionary<string, object?>();
        }

        /// <summary>
        /// Use for extension to get additional config values not in this object
        /// Returns null if not found
        /// </summary>
        /// <typeparam name="T">The expected type</typeparam>
        /// <param name="name">The name of the parameter</param>
        /// <returns></returns>
        public T GetAdditionalConfigValue<T>(string name) => restParameters.GetValue<T>(name);

        internal static Method GetFromConfig(string key, object value)
        {
            if (!Enum.TryParse(key, true, out OperationType operationType))
            {
                throw new ArgumentException("Key must be a Http Method");
            }
            if (value is string s)
                return new Method(operationType, s);
            var childConfig = (IReadOnlyDictionary<string, object?>)value;
            return new Method(operationType, childConfig.GetValue<string>("SP"),
                childConfig.GetValue<string?>("OperationId"),
                childConfig.GetValue<string?>("OperationName"),
                childConfig.GetValue<string?>("ResultType"),
                childConfig.GetValue<string?>("Tag"),
                childConfig.GetValue<int?>("CommandTimeout"),
                executeParameters: childConfig.GetValue<IReadOnlyDictionary<string, object?>?>("ExecuteParameters"),
                ignoreParameters: childConfig.GetValue<IReadOnlyCollection<string>?>("IgnoreParameters"),
                ignoreFields: childConfig.GetValue<IReadOnlyCollection<string>?>("IgnoreFields"),
                restParameters: childConfig);

        }

        /// <summary>
        /// Return the Http Method as string. Mainly for debugging
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return HttpMethod + " (" + SP + ")";
        }

    }
}
