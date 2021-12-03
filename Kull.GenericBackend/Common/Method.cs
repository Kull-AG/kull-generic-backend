using Kull.GenericBackend.Config;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;

namespace Kull.GenericBackend.Common;

/// <summary>
/// Represents an HTTP Method that is mapped to a Stored Procedure
/// </summary>
public record Method
{
    /// <summary>
    /// The Http Method
    /// </summary>
    public OperationType HttpMethod { get; init; }


    public Data.DBObjectName DbObject { get; init; }

    public Kull.DatabaseMetadata.DBObjectType DbObjectType { get; init; }

    public string? OperationId { get; init; }

    public string? OperationName { get; init; }
    public string? ParameterSchemaName { get; init; }

    public string? ResultSchemaName { get; init; }

    public string? ResultType { get; init; }
    public string? Tag { get; init; }

    public int? CommandTimeout { get; init; }

    private IReadOnlyDictionary<string, object?> restParameters;

    public IReadOnlyDictionary<string, object?>? ExecuteParameters { get; init; }

    public IReadOnlyCollection<string> IgnoreParameters { get; init; }
    public IReadOnlyCollection<string> IgnoreFields { get; init; }
    public IReadOnlyCollection<string> JsonFields { get; init; } = Array.Empty<string>();

    public Method(OperationType httpMethod, string sp)
        : this(httpMethod, sp, DatabaseMetadata.DBObjectType.StoredProcedure, null, null, null)
    {

    }

    private Method(OperationType httpMethod, string dbObjectName,
          Kull.DatabaseMetadata.DBObjectType dBObjectType,
        string? operationId = null,
        string? operationName = null,
        string? resultType = null, string? tag = null,
        int? commandTimeout = null,
        IReadOnlyDictionary<string, object?>? executeParameters = null,
        IReadOnlyCollection<string>? ignoreParameters = null,
        IReadOnlyDictionary<string, object?>? restParameters = null,
        IReadOnlyCollection<string>? ignoreFields = null)
    {
        if (dbObjectName == null) throw new ArgumentNullException("dbObjectName");
        HttpMethod = httpMethod;
        DbObject = dbObjectName;
        DbObjectType = dBObjectType;
        if (dBObjectType != DatabaseMetadata.DBObjectType.StoredProcedure && httpMethod != OperationType.Get)
        {
            throw new InvalidOperationException("Cannot use method other then GET for " + dBObjectType + " (object " + dbObjectName + ")");
        }
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
        string objectName;
        Kull.DatabaseMetadata.DBObjectType dBObjectType;
        if (childConfig.ContainsKey("View"))
        {
            objectName = childConfig.GetValue<string>("View");
            dBObjectType = DatabaseMetadata.DBObjectType.TableOrView;
        }
        else if (childConfig.ContainsKey("SP"))
        {
            objectName = childConfig.GetValue<string>("SP");
            dBObjectType = DatabaseMetadata.DBObjectType.StoredProcedure;
        }
        else if (childConfig.ContainsKey("Function"))
        {
            objectName = childConfig.GetValue<string>("Function");
            dBObjectType = DatabaseMetadata.DBObjectType.TableValuedFunction;
        }
        else
        {
            throw new InvalidOperationException("Must provide SP or View (or Function, which is in alpha)");
        }
        return new Method(operationType, objectName,
            dBObjectType,
            childConfig.GetValue<string?>("OperationId"),
            childConfig.GetValue<string?>("OperationName"),
            childConfig.GetValue<string?>("ResultType"),
            childConfig.GetValue<string?>("Tag"),
            childConfig.GetValue<int?>("CommandTimeout"),
            executeParameters: childConfig.GetValue<IReadOnlyDictionary<string, object?>?>("ExecuteParameters"),
            ignoreParameters: childConfig.GetValue<IReadOnlyCollection<string>?>("IgnoreParameters"),
            ignoreFields: childConfig.GetValue<IReadOnlyCollection<string>?>("IgnoreFields"),
            restParameters: childConfig)
        {
            ParameterSchemaName = childConfig.GetValue<string?>(nameof(ParameterSchemaName)),
            ResultSchemaName = childConfig.GetValue<string?>(nameof(ResultSchemaName)),
            JsonFields = childConfig.GetValue<IReadOnlyCollection<string>?>(nameof(JsonFields)) ?? Array.Empty<string>()
        };

    }

    /// <summary>
    /// Return the Http Method as string. Mainly for debugging
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return HttpMethod + " (" + DbObject + ")";
    }

}
