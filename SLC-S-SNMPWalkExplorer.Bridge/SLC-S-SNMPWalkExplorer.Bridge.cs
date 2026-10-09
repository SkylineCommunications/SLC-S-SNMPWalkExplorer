namespace SLCSSNMPWalkExplorerApi
{
using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Skyline.DataMiner.Automation;

/// <summary>
/// Provides non-interactive configuration operations for the SNMP Walk Explorer.
/// </summary>
public class Script
{
/// <summary>
/// Runs the requested bridge operation.
/// </summary>
/// <param name="engine">The Automation engine.</param>
public void Run(IEngine engine)
{
try
{
RunSafe(engine);
}
catch (ScriptAbortException)
{
throw;
}
catch (ScriptForceAbortException)
{
throw;
}
catch (ScriptTimeoutException)
{
throw;
}
catch (Exception exception)
{
string message = exception.Message;
if (exception.InnerException != null)
{
message += " (Inner: " + exception.InnerException.Message + ")";
}

engine.ExitFail("SNMP Walk Explorer bridge failed: " + message);
}
}

private static void RunSafe(IEngine engine)
{
string action = GetRequiredParameterValue(engine, "Action");
string result;

switch (action)
{
case "ListConfigurations":
result = Serialize(new WalkConfigurationRepository(engine).List());
break;
case "CreateConfiguration":
result = Serialize(new WalkConfigurationRepository(engine).Create(Deserialize<WalkConfiguration>(GetRequiredParameterValue(engine, "RequestJson"))));
break;
case "UpdateConfiguration":
result = Serialize(new WalkConfigurationRepository(engine).Update(Deserialize<WalkConfiguration>(GetRequiredParameterValue(engine, "RequestJson"))));
break;
case "DeleteConfiguration":
new WalkConfigurationRepository(engine).Delete(Deserialize<ConfigurationIdRequest>(GetRequiredParameterValue(engine, "RequestJson")).Id);
result = "{\"success\":true}";
break;
case "ListArtifacts":
result = new WalkArtifactCatalog(WalkArtifactCatalog.DefaultArtifactDirectory).SerializeArtifacts();
break;
case "GetArtifactTree":
result = GetArtifactTree(Deserialize<ArtifactRequest>(GetRequiredParameterValue(engine, "RequestJson")));
break;
case "SearchBindings":
result = SearchBindings(Deserialize<BindingSearchRequest>(GetRequiredParameterValue(engine, "RequestJson")));
break;
case "DownloadArtifact":
result = DownloadArtifact(Deserialize<ArtifactRequest>(GetRequiredParameterValue(engine, "RequestJson")));
break;
case "DeleteArtifact":
DeleteArtifact(Deserialize<ArtifactRequest>(GetRequiredParameterValue(engine, "RequestJson")));
result = "{\"success\":true}";
break;
case "ExecuteWalk":
result = ExecuteWalk(engine, Deserialize<WalkExecutionRequest>(GetRequiredParameterValue(engine, "RequestJson")));
break;
default:
throw new ArgumentException("Unsupported bridge action: " + action + ".");
}

ScriptParam resultParameter = engine.GetScriptParam("Result");
if (resultParameter != null)
{
resultParameter.SetParamValue(result);
}

try
{
engine.AddScriptOutput("Result", result);
}
catch
{
// Ignore if not supported in test runner or mock engine context
}
}

private static string GetArtifactTree(ArtifactRequest request)
{
string tree;
if (request == null || !new WalkArtifactCatalog(WalkArtifactCatalog.DefaultArtifactDirectory).TryBuildTree(request.ArtifactId, out tree))
{
throw new ArgumentException("The requested committed walk artifact was not found.");
}

return tree;
}

private static string SearchBindings(BindingSearchRequest request)
{
string bindings;
if (request == null || !new WalkArtifactCatalog(WalkArtifactCatalog.DefaultArtifactDirectory).TrySearchBindings(request.ArtifactId, request.Prefix, request.Query, out bindings))
{
throw new ArgumentException("The requested committed walk artifact was not found.");
}

return bindings;
}

private static string DownloadArtifact(ArtifactRequest request)
{
string rawArtifact;
if (request == null || !new WalkArtifactCatalog(WalkArtifactCatalog.DefaultArtifactDirectory).TryReadRawArtifact(request.ArtifactId, out rawArtifact))
{
throw new ArgumentException("The requested committed walk artifact was not found.");
}

return rawArtifact;
}

private static void DeleteArtifact(ArtifactRequest request)
{
if (request == null || String.IsNullOrWhiteSpace(request.ArtifactId))
{
throw new ArgumentException("The artifact ID is required.");
}

if (!new WalkArtifactCatalog(WalkArtifactCatalog.DefaultArtifactDirectory).TryDeleteArtifact(request.ArtifactId))
{
throw new ArgumentException("The requested committed walk artifact was not found or could not be deleted.");
}
}

private static string ExecuteWalk(IEngine engine, WalkExecutionRequest request)
{
if (request == null)
{
throw new ArgumentException("Execution request parameters are required.");
}

if (String.IsNullOrWhiteSpace(request.TargetAddress))
{
throw new ArgumentException("Target address is required.");
}

string correlationId = !String.IsNullOrWhiteSpace(request.RunCorrelationId)
? request.RunCorrelationId
: "walk_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

SubScriptOptions subScript = engine.PrepareSubScript("SLC-S-SNMPWalkCollector");
subScript.Synchronous = false;
subScript.SelectScriptParam("TargetAddress", request.TargetAddress.Trim());
subScript.SelectScriptParam("TargetPort", (request.TargetPort > 0 ? request.TargetPort : 161).ToString(CultureInfo.InvariantCulture));
subScript.SelectScriptParam("SnmpCommunity", request.SnmpCommunity ?? String.Empty);
subScript.SelectScriptParam("TimeoutMilliseconds", (request.TimeoutMilliseconds > 0 ? request.TimeoutMilliseconds : 5000).ToString(CultureInfo.InvariantCulture));
subScript.SelectScriptParam("Retries", (request.Retries >= 0 ? request.Retries : 2).ToString(CultureInfo.InvariantCulture));
subScript.SelectScriptParam("LogLevel", (request.LogLevel >= 0 ? request.LogLevel : 1).ToString(CultureInfo.InvariantCulture));
subScript.SelectScriptParam("MaximumWalkVariables", (request.MaximumWalkVariables > 0 ? request.MaximumWalkVariables : 100000).ToString(CultureInfo.InvariantCulture));
subScript.SelectScriptParam("ConcurrentWalkWorkers", (request.ConcurrentWalkWorkers > 0 ? request.ConcurrentWalkWorkers : 4).ToString(CultureInfo.InvariantCulture));
subScript.SelectScriptParam("UseGetBulk", request.UseGetBulk ? "true" : "false");
subScript.SelectScriptParam("BulkMaxRepetitions", (request.BulkMaxRepetitions > 0 ? request.BulkMaxRepetitions : 25).ToString(CultureInfo.InvariantCulture));
subScript.SelectScriptParam("PartitionRecommendationBindings", (request.PartitionRecommendationBindings > 0 ? request.PartitionRecommendationBindings : 1000).ToString(CultureInfo.InvariantCulture));
subScript.SelectScriptParam("GetBulkDiagnosticOid", request.GetBulkDiagnosticOid ?? String.Empty);
subScript.SelectScriptParam("DiscoveryRoots", request.DiscoveryRoots ?? String.Empty);
subScript.SelectScriptParam("RunCorrelationId", correlationId);

subScript.StartScript();

return "{\"success\":true,\"correlationId\":\"" + correlationId + "\"}";
}

private static string GetRequiredParameterValue(IEngine engine, string parameterName)
{
ScriptParam parameter = engine.GetScriptParam(parameterName);
if (parameter == null)
{
throw new InvalidOperationException("Required script parameter is unavailable: " + parameterName + ".");
}

return parameter.Value;
}

private static T Deserialize<T>(string json)
{
using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? String.Empty)))
{
return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
}
}

private static string Serialize<T>(T value)
{
using (MemoryStream stream = new MemoryStream())
{
new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
return Encoding.UTF8.GetString(stream.ToArray());
}
}

[DataContract]
internal class ArtifactRequest
{
[DataMember(Name = "artifactId")]
public string ArtifactId { get; set; }
}

[DataContract]
internal class ConfigurationIdRequest
{
[DataMember(Name = "id")]
public string Id { get; set; }
}

[DataContract]
internal sealed class WalkExecutionRequest
{
[DataMember(Name = "targetAddress")]
public string TargetAddress { get; set; }

[DataMember(Name = "targetPort")]
public int TargetPort { get; set; }

[DataMember(Name = "snmpCommunity")]
public string SnmpCommunity { get; set; }

[DataMember(Name = "timeoutMilliseconds")]
public int TimeoutMilliseconds { get; set; }

[DataMember(Name = "retries")]
public int Retries { get; set; }

[DataMember(Name = "logLevel")]
public int LogLevel { get; set; }

[DataMember(Name = "maximumWalkVariables")]
public int MaximumWalkVariables { get; set; }

[DataMember(Name = "concurrentWalkWorkers")]
public int ConcurrentWalkWorkers { get; set; }

[DataMember(Name = "useGetBulk")]
public bool UseGetBulk { get; set; }

[DataMember(Name = "bulkMaxRepetitions")]
public int BulkMaxRepetitions { get; set; }

[DataMember(Name = "partitionRecommendationBindings")]
public int PartitionRecommendationBindings { get; set; }

[DataMember(Name = "getBulkDiagnosticOid")]
public string GetBulkDiagnosticOid { get; set; }

[DataMember(Name = "discoveryRoots")]
public string DiscoveryRoots { get; set; }

[DataMember(Name = "runCorrelationId")]
public string RunCorrelationId { get; set; }
}

[DataContract]
internal sealed class BindingSearchRequest : ArtifactRequest
{
[DataMember(Name = "prefix")]
public string Prefix { get; set; }

[DataMember(Name = "query")]
public string Query { get; set; }
}
}
}