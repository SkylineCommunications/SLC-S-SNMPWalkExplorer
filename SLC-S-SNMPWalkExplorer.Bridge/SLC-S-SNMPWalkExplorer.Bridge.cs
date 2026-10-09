namespace SLCSSNMPWalkExplorerApi
{
using System;
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
internal sealed class BindingSearchRequest : ArtifactRequest
{
[DataMember(Name = "prefix")]
public string Prefix { get; set; }

[DataMember(Name = "query")]
public string Query { get; set; }
}
}
}