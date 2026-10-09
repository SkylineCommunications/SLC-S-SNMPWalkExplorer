namespace SLCSSNMPWalkCollector
{
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Skyline.DataMiner.Automation;
/// <summary>
/// Collects an SNMP walk into a text file on the executing DataMiner Agent.
/// </summary>
public class Script
{
private const string OutputDirectory = @"C:\Skyline DataMiner\Documents\SLC-S-SNMPWalkCollector";

// Source-controlled execution settings.
private const string QaSnmpAssemblyPath = @"C:\Skyline DataMiner\Tools\QADeviceSimulator\Skyline.DataMiner.QA.SNMP.dll";

/// <summary>
/// Runs the configured SNMP collection.
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
engine.ExitFail("SNMP walk failed: " + exception.Message);
}
}

private static void RunSafe(IEngine engine)
{
WalkRunSettingsSnapshot snapshot = WalkRunSettingsSnapshot.CreateFromParameterValues(
GetScriptParameterValue(engine, "TargetAddress"),
GetScriptParameterValue(engine, "TargetPort"),
GetScriptParameterValue(engine, "SnmpCommunity"),
GetScriptParameterValue(engine, "TimeoutMilliseconds"),
GetScriptParameterValue(engine, "Retries"),
GetScriptParameterValue(engine, "LogLevel"),
GetScriptParameterValue(engine, "MaximumWalkVariables"),
GetScriptParameterValue(engine, "ConcurrentWalkWorkers"),
GetScriptParameterValue(engine, "UseGetBulk"),
GetScriptParameterValue(engine, "BulkMaxRepetitions"),
GetScriptParameterValue(engine, "PartitionRecommendationBindings"),
GetScriptParameterValue(engine, "GetBulkDiagnosticOid"),
GetScriptParameterValue(engine, "DiscoveryRoots"),
GetScriptParameterValue(engine, "RunCorrelationId"));
WalkSettings settings = WalkSettings.Create(snapshot);
Trace(engine, settings, 1, "Starting SNMP walk collector using QA Device Simulator SNMP library.");
Trace(engine, settings, 2, "Configuration: target=" + settings.Address + ":" + settings.Port.ToString(CultureInfo.InvariantCulture) + "; protocolSelection=automatic; timeoutMs=" + settings.TimeoutMilliseconds.ToString(CultureInfo.InvariantCulture) + "; retries=" + settings.Retries.ToString(CultureInfo.InvariantCulture) + "; concurrentWalkWorkers=" + settings.ConcurrentWalkWorkers.ToString(CultureInfo.InvariantCulture) + "; useGetBulk=" + settings.UseGetBulk.ToString() + "; partitionRecommendationBindings=" + settings.PartitionRecommendationBindings.ToString(CultureInfo.InvariantCulture) + "; communityLength=" + settings.Community.Length.ToString(CultureInfo.InvariantCulture) + "; library=" + QaSnmpAssemblyPath + ".");
QaSnmpClient client = QaSnmpClient.Create(QaSnmpAssemblyPath, settings);
ProbeResult version1Probe = Probe(engine, CollectorVersion.V1, client, settings);
ProbeResult version2Probe = Probe(engine, CollectorVersion.V2c, client, settings);

engine.GenerateInformation(version1Probe.ToLogMessage());
engine.GenerateInformation(version2Probe.ToLogMessage());

CollectorVersion selectedVersion = SelectHighestWorkingVersion(version1Probe, version2Probe);
Trace(engine, settings, 1, "Automatically selected " + GetVersionName(selectedVersion) + " for the full walk.");

if (!String.IsNullOrWhiteSpace(settings.GetBulkDiagnosticOid))
{
Trace(engine, settings, 1, client.GetBulkDiagnostic(selectedVersion, settings.GetBulkDiagnosticOid, settings.Retries));
}

string outputFile = GetOutputFilePath(settings.Address);
Trace(engine, settings, 1, "Starting " + GetVersionName(selectedVersion) + " discovery from roots " + String.Join(", ", settings.DiscoveryRoots) + ".");
WalkToFile(engine, client, settings, selectedVersion, outputFile);
Trace(engine, settings, 1, "Walk completed. Output file=" + outputFile + ".");
Trace(engine, settings, 1, "SNMP " + GetVersionName(selectedVersion) + " walk completed successfully.");
}

private static string GetScriptParameterValue(IEngine engine, string parameterName)
{
ScriptParam parameter = engine.GetScriptParam(parameterName);
if (parameter == null)
{
throw new InvalidOperationException("Required script parameter is unavailable: " + parameterName + ".");
}

return parameter.Value;
}

private static CollectorVersion SelectHighestWorkingVersion(ProbeResult version1Probe, ProbeResult version2Probe)
{
if (version2Probe.Succeeded)
{
return CollectorVersion.V2c;
}

if (version1Probe.Succeeded)
{
return CollectorVersion.V1;
}

throw new InvalidOperationException("SNMP probe results: " + version1Probe.ToLogMessage() + " " + version2Probe.ToLogMessage() + " Neither SNMPv1 nor SNMPv2c can be walked.");
}
private static ProbeResult Probe(IEngine engine, CollectorVersion version, QaSnmpClient client, WalkSettings settings)
{
try
{
Trace(engine, settings, 2, "Probing " + GetVersionName(version) + " using sysDescr.0.");
IList<WalkVariable> response = ExecuteWithRetry(engine, "GET " + GetVersionName(version), delegate { return client.Get(version, "1.3.6.1.2.1.1.1.0"); }, settings);
Trace(engine, settings, 2, "Probe response count for " + GetVersionName(version) + "=" + response.Count.ToString(CultureInfo.InvariantCulture) + ".");
return response.Count == 1 ? ProbeResult.Success(version) : ProbeResult.Failure(version, "The agent returned no value for the protocol probe.");
}
catch (Exception exception)
{
Trace(engine, settings, 1, "Probe failed for " + GetVersionName(version) + ": " + exception.Message);
return ProbeResult.Failure(version, exception.Message);
}
}

private static void WalkToFile(IEngine engine, QaSnmpClient client, WalkSettings settings, CollectorVersion version, string outputFile)
{
List<WalkVariable> variables = new List<WalkVariable>();
HashSet<string> publishedOids = new HashSet<string>(StringComparer.Ordinal);
DateTime startedAtUtc = DateTime.UtcNow;
List<RootWalkResult> rootResults = WalkPrefixes(engine, client, settings, version);

foreach (RootWalkResult rootResult in rootResults)
{
foreach (WalkVariable variable in rootResult.Variables)
{
if (publishedOids.Add(variable.Oid))
{
variables.Add(variable);
}
}

Trace(engine, settings, 1, "Prefix " + rootResult.RootOid + " returned " + rootResult.BindingCount.ToString(CultureInfo.InvariantCulture) + " variable bindings; terminalState=" + rootResult.TerminalState + ".");
}

Trace(engine, settings, 1, "Discovery returned " + variables.Count.ToString(CultureInfo.InvariantCulture) + " variable bindings.");
Directory.CreateDirectory(OutputDirectory);
string rawTemporaryFile = outputFile + ".partial";
string metadataFile = outputFile + ".metadata.json";
string metadataTemporaryFile = metadataFile + ".partial";

try
{
using (StreamWriter writer = new StreamWriter(rawTemporaryFile, false, new UTF8Encoding(false)))
{
foreach (WalkVariable variable in variables)
{
writer.WriteLine("{\"oid\":\"" + WalkPrimitives.EscapeJson(variable.Oid) + "\",\"value\":\"" + WalkPrimitives.EscapeJson(variable.Value) + "\"}");
}
}

File.Move(rawTemporaryFile, outputFile);

WalkMetadata metadata = new WalkMetadata(Path.GetFileName(outputFile), startedAtUtc, DateTime.UtcNow, settings.Address.ToString(), settings.Port, GetVersionName(version), settings.ConcurrentWalkWorkers, settings.UseGetBulk, settings.PartitionRecommendationBindings, settings.DiscoveryRoots, settings.RunCorrelationId, variables.Count, rootResults);
using (StreamWriter writer = new StreamWriter(metadataTemporaryFile, false, new UTF8Encoding(false)))
{
writer.Write(metadata.ToJson());
}

File.Move(metadataTemporaryFile, metadataFile);
}
catch
{
if (File.Exists(rawTemporaryFile))
{
File.Delete(rawTemporaryFile);
}

if (File.Exists(metadataTemporaryFile))
{
File.Delete(metadataTemporaryFile);
}

throw;
}

if (variables.Count == 0)
{
throw new InvalidOperationException("No variable bindings were collected. Root results: " + String.Join(" | ", rootResults.ConvertAll(delegate (RootWalkResult result) { return result.ToLogMessage(); })));
}
}

private static List<RootWalkResult> WalkPrefixes(IEngine engine, QaSnmpClient initialClient, WalkSettings settings, CollectorVersion version)
{
return QaSnmpClient.WalkPrefixes(
initialClient,
settings,
version,
delegate (int detail, string message) { Trace(engine, settings, detail, message); },
delegate { return QaSnmpClient.Create(QaSnmpAssemblyPath, settings); });
}

private static IList<WalkVariable> ExecuteWithRetry(IEngine engine, string operation, Func<IList<WalkVariable>> action, WalkSettings settings)
{
Exception lastException = null;
for (int attempt = 0; attempt <= settings.Retries; attempt++)
{
try
{
Trace(engine, settings, 3, operation + " attempt " + (attempt + 1).ToString(CultureInfo.InvariantCulture) + " of " + (settings.Retries + 1).ToString(CultureInfo.InvariantCulture) + ".");
IList<WalkVariable> result = action();
Trace(engine, settings, 3, operation + " attempt succeeded.");
return result;
}
catch (Exception exception)
{
lastException = exception;
Trace(engine, settings, 3, operation + " attempt failed: " + exception.Message);
}
}

throw new InvalidOperationException("No response after " + (settings.Retries + 1).ToString(CultureInfo.InvariantCulture) + " attempt(s).", lastException);
}

private static void Trace(IEngine engine, WalkSettings settings, int detail, string message)
{
if (settings.LogLevel >= detail)
{
engine.Log(message, LogType.Always, 5, "SNMPWalkCollector");
}
}

private static string GetOutputFilePath(IPAddress address)
{
string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
return Path.Combine(OutputDirectory, "SNMPWalk_" + address + "_" + timestamp + ".walk");
}

private static string GetVersionName(CollectorVersion version)
{
return version == CollectorVersion.V1 ? "SNMPv1" : "SNMPv2c";
}

internal static bool IsValidNumericOidForTesting(string oid)
{
return WalkPrimitives.IsValidOid(oid);
}

internal static bool TryCompareOidsForTesting(string left, string right, out int comparison)
{
return WalkPrimitives.TryCompareOids(left, right, out comparison);
}

internal static string EscapeJsonForTesting(string value)
{
return WalkPrimitives.EscapeJson(value);
}
}
}



















