using Kull.GenericBackend.Common;
using Kull.GenericBackend.Parameters;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kull.GenericBackend.SwaggerGeneration;

public class OperationResponseContext
{
    /// <summary>
    /// The entity, representing a url
    /// </summary>
    public Entity Entity { get; }

    /// <summary>
    /// The HTTP Method
    /// </summary>
    public Method Method { get; }

    /// <summary>
    /// This comes from the options and is set to true in case one must wrap Json Data in an object
    /// </summary>
    public bool AlwaysWrapJson { get; }

    public string? OutputObjectTypeName { get; }


    public string ResultTypeName { get; }

    /// <summary>
    /// The output parameters of the Procedure
    /// </summary>
    public IReadOnlyCollection<OutputParameter> OutputParameters { get; }

    internal OperationResponseContext(Entity ent, Method method, bool alwaysWrapJson,
        IReadOnlyCollection<OutputParameter> outputParameters,
        string resultTypeName,
        string? outputObjectTypeName)
    {
        this.Entity = ent;
        this.Method = method;
        AlwaysWrapJson = alwaysWrapJson;
        this.OutputParameters = outputParameters;
        this.ResultTypeName = resultTypeName;
        this.OutputObjectTypeName = outputObjectTypeName;
    }

}
