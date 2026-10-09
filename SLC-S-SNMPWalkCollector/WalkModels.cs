namespace SLCSSNMPWalkCollector
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Text;

    internal enum CollectorVersion
    {
        V1,
        V2c,
    }

    internal sealed class WalkVariable
    {
        public WalkVariable(string oid, string value)
        {
            Oid = oid;
            Value = value;
        }

        public string Oid { get; private set; }

        public string Value { get; private set; }
    }

    internal sealed class BulkTransportResponse
    {
        public BulkTransportResponse(IList<WalkVariable> variables, int errorStatus)
        {
            Variables = variables ?? new List<WalkVariable>();
            ErrorStatus = errorStatus;
        }

        public IList<WalkVariable> Variables { get; private set; }

        public int ErrorStatus { get; private set; }
    }

    internal interface ISnmpTransport
    {
        IList<WalkVariable> SendGet(CollectorVersion version, string oid);

        IList<WalkVariable> SendGetNext(CollectorVersion version, string oid);

        BulkTransportResponse SendGetBulk(string oid, int maxRepetitions);
    }

    internal sealed class CollectionBudget
    {
        private readonly object gate = new object();
        private int remaining;

        public CollectionBudget(int maximumVariables)
        {
            remaining = maximumVariables;
        }

        public int Remaining
        {
            get
            {
                lock (gate)
                {
                    return remaining;
                }
            }
        }

        public bool IsExhausted
        {
            get
            {
                lock (gate)
                {
                    return remaining == 0;
                }
            }
        }

        public bool TryReserve()
        {
            lock (gate)
            {
                if (remaining == 0)
                {
                    return false;
                }

                remaining--;
                return true;
            }
        }
    }

    internal sealed class GetBulkResult
    {
        private GetBulkResult(IList<WalkVariable> variables, string error, int errorStatus, string errorStatusName, int retryCount, bool isAuthenticationFailure)
        {
            Variables = variables;
            Error = error;
            ErrorStatus = errorStatus;
            ErrorStatusName = errorStatusName;
            RetryCount = retryCount;
            IsAuthenticationFailure = isAuthenticationFailure;
        }

        public string Error { get; private set; }

        public int ErrorStatus { get; private set; }

        public string ErrorStatusName { get; private set; }

        public bool IsAdaptiveFailure { get { return ErrorStatus == 1 || ErrorStatus == 5; } }

        public bool IsAuthenticationFailure { get; private set; }

        public bool IsUnsafeFallbackFailure { get { return IsAuthenticationFailure || (ErrorStatus != 0 && !IsAdaptiveFailure); } }

        public int RetryCount { get; private set; }

        public IList<WalkVariable> Variables { get; private set; }

        public static GetBulkResult AuthenticationFailure(string error, int retryCount)
        {
            return new GetBulkResult(new List<WalkVariable>(), error, 16, "authorizationError", retryCount, true);
        }

        public static GetBulkResult Failure(string error, int errorStatus, int retryCount, bool isAuthenticationFailure)
        {
            return new GetBulkResult(new List<WalkVariable>(), error, errorStatus, QaSnmpClient.GetPduErrorStatusName(errorStatus), retryCount, isAuthenticationFailure);
        }

        public static GetBulkResult Success(IList<WalkVariable> variables, int retryCount)
        {
            return new GetBulkResult(variables, null, 0, "noError", retryCount, false);
        }

        public GetBulkResult WithRetryCount(int retryCount)
        {
            return new GetBulkResult(Variables, Error, ErrorStatus, ErrorStatusName, retryCount, IsAuthenticationFailure);
        }
    }

    internal sealed class GetNextResult
    {
        private GetNextResult(IList<WalkVariable> variables, string error, int retryCount)
        {
            Variables = variables;
            Error = error;
            RetryCount = retryCount;
        }

        public string Error { get; private set; }

        public int RetryCount { get; private set; }

        public IList<WalkVariable> Variables { get; private set; }

        public static GetNextResult Failure(string error, int retryCount)
        {
            return new GetNextResult(new List<WalkVariable>(), error, retryCount);
        }

        public static GetNextResult Success(IList<WalkVariable> variables, int retryCount)
        {
            return new GetNextResult(variables, null, retryCount);
        }
    }

    internal sealed class WalkSettings
    {
        public IPAddress Address { get; private set; }

        public string Community { get; private set; }

        public int LogLevel { get; private set; }

        public int Port { get; private set; }

        public int Retries { get; private set; }

        public int TimeoutMilliseconds { get; private set; }

        public int MaximumWalkVariables { get; private set; }

        public int BulkMaxRepetitions { get; private set; }

        public int ConcurrentWalkWorkers { get; private set; }

        public string GetBulkDiagnosticOid { get; private set; }

        public bool UseGetBulk { get; private set; }

        public int PartitionRecommendationBindings { get; private set; }

        public string[] DiscoveryRoots { get; private set; }

        public string RunCorrelationId { get; private set; }

        public static WalkSettings Create(WalkRunSettingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException("snapshot");
            }

            return new WalkSettings
            {
                Address = snapshot.Connection.Address,
                BulkMaxRepetitions = snapshot.BulkMaxRepetitions,
                ConcurrentWalkWorkers = snapshot.ConcurrentWalkWorkers,
                DiscoveryRoots = snapshot.DiscoveryRoots,
                GetBulkDiagnosticOid = snapshot.GetBulkDiagnosticOid,
                UseGetBulk = snapshot.UseGetBulk,
                Community = snapshot.Connection.Community,
                LogLevel = snapshot.LogLevel,
                MaximumWalkVariables = snapshot.MaximumWalkVariables,
                PartitionRecommendationBindings = snapshot.PartitionRecommendationBindings,
                RunCorrelationId = snapshot.RunCorrelationId,
                Port = snapshot.Connection.Port,
                Retries = snapshot.Retries,
                TimeoutMilliseconds = snapshot.TimeoutMilliseconds,
            };
        }
    }

    internal sealed class RootWalkResult
    {
        public RootWalkResult(string rootOid, IList<WalkVariable> variables, string lastOid, string terminalState, string error, int retryCount)
        {
            RootOid = rootOid;
            Variables = new List<WalkVariable>(variables ?? new WalkVariable[0]);
            LastOid = lastOid;
            TerminalState = terminalState;
            Error = error;
            RetryCount = retryCount;
        }

        public int BindingCount { get { return Variables.Count; } }

        public string Error { get; private set; }

        public string LastOid { get; private set; }

        public int RetryCount { get; private set; }

        public string RootOid { get; private set; }

        public string TerminalState { get; private set; }

        public IList<WalkVariable> Variables { get; private set; }

        public static RootWalkResult SkippedGlobalSafetyCap(string rootOid)
        {
            return new RootWalkResult(rootOid, new List<WalkVariable>(), null, "skipped-global-safety-cap", "The global walk safety cap was reached before this root started.", 0);
        }

        public string ToLogMessage()
        {
            return RootOid + ": " + TerminalState + "; bindings=" + BindingCount.ToString(CultureInfo.InvariantCulture) + (String.IsNullOrEmpty(Error) ? String.Empty : "; error=" + Error);
        }
    }

    internal sealed class RootWalkOutcome
    {
        public RootWalkOutcome(RootWalkResult result, int partitionRecommendationBindings)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            RootOid = result.RootOid;
            BindingCount = result.BindingCount;
            LastOid = result.LastOid;
            TerminalState = result.TerminalState;
            Error = result.Error;
            RetryCount = result.RetryCount;
            PartitionRecommendationBindings = partitionRecommendationBindings;
        }

        public int BindingCount { get; private set; }

        public string Error { get; private set; }

        public string LastOid { get; private set; }

        public int RetryCount { get; private set; }

        public string RootOid { get; private set; }

        public string TerminalState { get; private set; }

        public int PartitionRecommendationBindings { get; private set; }

        public bool IsComplete { get { return TerminalState == "outside-subtree" || TerminalState == "end-of-mib"; } }

        public bool PartitionRecommended { get { return BindingCount >= PartitionRecommendationBindings && IsComplete; } }

        public string ToJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('{');
            AppendProperty(builder, "rootOid", RootOid, true);
            AppendProperty(builder, "bindingCount", BindingCount.ToString(CultureInfo.InvariantCulture), false);
            AppendProperty(builder, "lastOid", LastOid, true);
            AppendProperty(builder, "terminalState", TerminalState, true);
            AppendProperty(builder, "isComplete", IsComplete ? "true" : "false", false);
            AppendProperty(builder, "partitionRecommended", PartitionRecommended ? "true" : "false", false);
            if (!String.IsNullOrEmpty(Error))
            {
                AppendProperty(builder, "error", Error, true);
            }

            AppendProperty(builder, "retryCount", RetryCount.ToString(CultureInfo.InvariantCulture), false);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendProperty(StringBuilder builder, string name, string value, bool quoteValue)
        {
            if (builder.Length > 1)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(WalkPrimitives.EscapeJson(name)).Append("\":");
            if (quoteValue)
            {
                builder.Append('"').Append(WalkPrimitives.EscapeJson(value)).Append('"');
            }
            else
            {
                builder.Append(value);
            }
        }
    }

    internal sealed class WalkMetadata
    {
        public WalkMetadata(string rawFileName, DateTime startedAtUtc, DateTime completedAtUtc, string targetAddress, int targetPort, string snmpVersion, int concurrentWalkWorkers, bool useGetBulk, int partitionRecommendationBindings, string[] discoveryRoots, string runCorrelationId, int totalBindings, IList<RootWalkResult> rootResults)
        {
            RawFileName = rawFileName;
            StartedAtUtc = startedAtUtc.ToString("o", CultureInfo.InvariantCulture);
            CompletedAtUtc = completedAtUtc.ToString("o", CultureInfo.InvariantCulture);
            TargetAddress = targetAddress;
            TargetPort = targetPort;
            SnmpVersion = snmpVersion;
            ConcurrentWalkWorkers = concurrentWalkWorkers;
            UseGetBulk = useGetBulk;
            PartitionRecommendationBindings = partitionRecommendationBindings;
            DiscoveryRoots = discoveryRoots ?? new string[0];
            RunCorrelationId = runCorrelationId;
            TotalBindings = totalBindings;
            RootOutcomes = new List<RootWalkOutcome>();

            if (rootResults != null)
            {
                foreach (RootWalkResult rootResult in rootResults)
                {
                    RootOutcomes.Add(new RootWalkOutcome(rootResult, partitionRecommendationBindings));
                }
            }
        }

        public string CompletedAtUtc { get; private set; }

        public int ConcurrentWalkWorkers { get; private set; }

        public string[] DiscoveryRoots { get; private set; }

        public string RawFileName { get; private set; }

        public IList<RootWalkOutcome> RootOutcomes { get; private set; }

        public string SnmpVersion { get; private set; }

        public string StartedAtUtc { get; private set; }

        public string TargetAddress { get; private set; }

        public int TargetPort { get; private set; }

        public int TotalBindings { get; private set; }

        public bool UseGetBulk { get; private set; }

        public int PartitionRecommendationBindings { get; private set; }

        public string RunCorrelationId { get; private set; }

        public string ToJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('{');
            AppendJsonProperty(builder, "schemaVersion", "2", false);
            AppendJsonProperty(builder, "rawFileName", RawFileName, true);
            AppendJsonProperty(builder, "rawFormat", "jsonl", true);
            AppendJsonProperty(builder, "runCorrelationId", RunCorrelationId, true);
            builder.Append("\"discoveryRoots\":[");
            for (int index = 0; index < DiscoveryRoots.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append('"').Append(WalkPrimitives.EscapeJson(DiscoveryRoots[index])).Append('"');
            }

            builder.Append("],");
            AppendJsonProperty(builder, "startedAtUtc", StartedAtUtc, true);
            AppendJsonProperty(builder, "completedAtUtc", CompletedAtUtc, true);
            AppendJsonProperty(builder, "targetAddress", TargetAddress, true);
            AppendJsonProperty(builder, "targetPort", TargetPort.ToString(CultureInfo.InvariantCulture), false);
            AppendJsonProperty(builder, "snmpVersion", SnmpVersion, true);
            AppendJsonProperty(builder, "concurrentWalkWorkers", ConcurrentWalkWorkers.ToString(CultureInfo.InvariantCulture), false);
            AppendJsonProperty(builder, "useGetBulk", UseGetBulk ? "true" : "false", false);
            AppendJsonProperty(builder, "partitionRecommendationBindings", PartitionRecommendationBindings.ToString(CultureInfo.InvariantCulture), false);
            AppendJsonProperty(builder, "totalBindings", TotalBindings.ToString(CultureInfo.InvariantCulture), false);
            AppendJsonProperty(builder, "isComplete", IsComplete() ? "true" : "false", false);
            builder.Append(",\"rootOutcomes\":[");

            for (int index = 0; index < RootOutcomes.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(RootOutcomes[index].ToJson());
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private bool IsComplete()
        {
            foreach (RootWalkOutcome outcome in RootOutcomes)
            {
                if (!outcome.IsComplete)
                {
                    return false;
                }
            }

            return true;
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool quoteValue)
        {
            if (builder.Length > 1)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(WalkPrimitives.EscapeJson(name)).Append("\":");
            if (quoteValue)
            {
                builder.Append('"').Append(WalkPrimitives.EscapeJson(value)).Append('"');
            }
            else
            {
                builder.Append(value);
            }
        }
    }

    internal sealed class ProbeResult
    {
        private ProbeResult(CollectorVersion version, bool succeeded, string error)
        {
            Version = version;
            Succeeded = succeeded;
            Error = error;
        }

        public string Error { get; private set; }

        public bool Succeeded { get; private set; }

        public CollectorVersion Version { get; private set; }

        public static ProbeResult Failure(CollectorVersion version, string error)
        {
            return new ProbeResult(version, false, error);
        }

        public static ProbeResult Success(CollectorVersion version)
        {
            return new ProbeResult(version, true, String.Empty);
        }

        public string ToLogMessage()
        {
            return (Version == CollectorVersion.V1 ? "SNMPv1" : "SNMPv2c") + " probe " + (Succeeded ? "passed." : "failed: " + Error);
        }
    }
}
