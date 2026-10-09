namespace SLCSSNMPWalkCollector
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;

    internal sealed class QaSnmpClient
    {
        private readonly ISnmpTransport transport;

        public QaSnmpClient(ISnmpTransport transport)
        {
            if (transport == null)
            {
                throw new ArgumentNullException("transport");
            }

            this.transport = transport;
        }

        public static QaSnmpClient Create(string assemblyPath, WalkSettings settings)
        {
            return new QaSnmpClient(QaSnmpReflectionTransport.Create(assemblyPath, settings));
        }

        public IList<WalkVariable> Get(CollectorVersion version, string oid)
        {
            return transport.SendGet(version, oid);
        }

        public string GetBulkDiagnostic(CollectorVersion version, string oid, int retries)
        {
            if (version != CollectorVersion.V2c)
            {
                return "GETBULK diagnostic skipped because the selected version is " + (version == CollectorVersion.V1 ? "SNMPv1" : "SNMPv2c") + ".";
            }

            GetBulkResult result = GetBulkWithRetry(oid, 1, retries, delegate { });
            return result.Error == null ? "GETBULK diagnostic succeeded at " + oid + " with " + result.Variables.Count.ToString(CultureInfo.InvariantCulture) + " binding(s)." : "GETBULK diagnostic failed at " + oid + ": " + result.Error;
        }

        public RootWalkResult Walk(CollectorVersion version, string rootOid, int retries, bool useGetBulk, int bulkMaxRepetitions, Func<bool> tryReserve, Action<string> trace)
        {
            List<WalkVariable> variables = new List<WalkVariable>();
            HashSet<string> observedOids = new HashSet<string>(StringComparer.Ordinal);
            string currentOid = rootOid;
            int retryCount = 0;

            while (true)
            {
                if (version == CollectorVersion.V2c && useGetBulk)
                {
                    GetBulkResult getBulkResult = GetBulkWithAdaptiveFallback(currentOid, bulkMaxRepetitions, retries, trace);
                    retryCount += getBulkResult.RetryCount;

                    if (getBulkResult.Error == null && getBulkResult.Variables.Count > 0)
                    {
                        foreach (WalkVariable variable in getBulkResult.Variables)
                        {
                            RootWalkResult terminalResult = AcceptWalkVariable(rootOid, variables, observedOids, ref currentOid, variable, retryCount, tryReserve);
                            if (terminalResult != null)
                            {
                                return terminalResult;
                            }
                        }

                        continue;
                    }

                    if (getBulkResult.Error != null && getBulkResult.IsUnsafeFallbackFailure)
                    {
                        return new RootWalkResult(rootOid, variables, currentOid, "request-failed", getBulkResult.Error, retryCount);
                    }

                    trace("GETBULK SNMPv2c at " + currentOid + " falling back to GETNEXT: " + (getBulkResult.Error ?? "The QA SNMP library returned an empty response.") + ".");
                }

                GetNextResult getNextResult = GetNextWithRetry(version, currentOid, retries, trace);
                retryCount += getNextResult.RetryCount;

                if (getNextResult.Error != null)
                {
                    return new RootWalkResult(rootOid, variables, currentOid, "request-failed", getNextResult.Error, retryCount);
                }

                if (getNextResult.Variables.Count != 1)
                {
                    return new RootWalkResult(rootOid, variables, currentOid, "unexpected-binding-count", "The QA SNMP library returned " + getNextResult.Variables.Count.ToString(CultureInfo.InvariantCulture) + " bindings for GetNext.", retryCount);
                }

                RootWalkResult getNextTerminalResult = AcceptWalkVariable(rootOid, variables, observedOids, ref currentOid, getNextResult.Variables[0], retryCount, tryReserve);
                if (getNextTerminalResult != null)
                {
                    return getNextTerminalResult;
                }
            }
        }

        public RootWalkResult AcceptWalkVariable(string rootOid, IList<WalkVariable> variables, ISet<string> observedOids, ref string currentOid, WalkVariable variable, int retryCount, Func<bool> tryReserve)
        {
            if (!WalkPrimitives.IsValidOid(variable.Oid))
            {
                return new RootWalkResult(rootOid, variables, currentOid, "invalid-oid", "The SNMP agent returned an invalid OID: " + variable.Oid + ".", retryCount);
            }

            if (IsEndOfMibValue(variable.Value))
            {
                return new RootWalkResult(rootOid, variables, currentOid, "end-of-mib", null, retryCount);
            }

            if (!variable.Oid.StartsWith(rootOid + ".", StringComparison.Ordinal))
            {
                return new RootWalkResult(rootOid, variables, currentOid, "outside-subtree", null, retryCount);
            }

            if (observedOids.Contains(variable.Oid))
            {
                return new RootWalkResult(rootOid, variables, currentOid, "repeated-oid", "The SNMP agent returned a repeated OID sequence at " + variable.Oid + ".", retryCount);
            }

            int comparison;
            if (!WalkPrimitives.TryCompareOids(variable.Oid, currentOid, out comparison))
            {
                return new RootWalkResult(rootOid, variables, currentOid, "invalid-oid", "The SNMP agent returned an invalid OID.", retryCount);
            }

            if (comparison <= 0)
            {
                return new RootWalkResult(rootOid, variables, currentOid, "non-increasing-oid", "The SNMP agent returned a non-increasing OID: " + variable.Oid + ".", retryCount);
            }

            if (!tryReserve())
            {
                return new RootWalkResult(rootOid, variables, currentOid, "global-safety-cap", "The global walk safety cap was reached.", retryCount);
            }

            variables.Add(variable);
            observedOids.Add(variable.Oid);
            currentOid = variable.Oid;
            return null;
        }

        public GetBulkResult GetBulkWithAdaptiveFallback(string oid, int configuredMaxRepetitions, int retries, Action<string> trace)
        {
            int maxRepetitions = configuredMaxRepetitions;
            int retryCount = 0;

            while (true)
            {
                GetBulkResult result = GetBulkWithRetry(oid, maxRepetitions, retries, trace);
                retryCount += result.RetryCount;

                if (result.Error == null || result.IsAuthenticationFailure || !result.IsAdaptiveFailure || maxRepetitions == 1)
                {
                    return result.WithRetryCount(retryCount);
                }

                int reducedMaxRepetitions = Math.Max(1, maxRepetitions / 2);
                retryCount++;
                trace("GETBULK SNMPv2c at " + oid + " returned " + result.ErrorStatusName + "; reducing max repetitions from " + maxRepetitions.ToString(CultureInfo.InvariantCulture) + " to " + reducedMaxRepetitions.ToString(CultureInfo.InvariantCulture) + ".");
                maxRepetitions = reducedMaxRepetitions;
            }
        }

        public GetBulkResult GetBulkWithRetry(string oid, int maxRepetitions, int retries, Action<string> trace)
        {
            Exception lastException = null;
            for (int attempt = 0; attempt <= retries; attempt++)
            {
                try
                {
                    trace("GETBULK SNMPv2c at " + oid + " with max repetitions=" + maxRepetitions.ToString(CultureInfo.InvariantCulture) + " attempt " + (attempt + 1).ToString(CultureInfo.InvariantCulture) + " of " + (retries + 1).ToString(CultureInfo.InvariantCulture) + ".");
                    BulkTransportResponse response = transport.SendGetBulk(oid, maxRepetitions);
                    if (response.ErrorStatus != 0)
                    {
                        return GetBulkResult.Failure(GetPduErrorStatusName(response.ErrorStatus), response.ErrorStatus, attempt, false);
                    }

                    SortVariablesNumerically(response.Variables);
                    return GetBulkResult.Success(response.Variables, attempt);
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    trace("GETBULK SNMPv2c at " + oid + " attempt failed: " + exception.Message);
                    if (IsAuthenticationException(exception))
                    {
                        return GetBulkResult.AuthenticationFailure(exception.Message, attempt);
                    }
                }
            }

            return GetBulkResult.Failure("No response for GetBulk at OID " + oid + " after " + (retries + 1).ToString(CultureInfo.InvariantCulture) + " attempt(s). Last error: " + (lastException != null ? lastException.Message : "Unknown error."), 0, retries, false);
        }

        public GetNextResult GetNextWithRetry(CollectorVersion version, string oid, int retries, Action<string> trace)
        {
            Exception lastException = null;
            for (int attempt = 0; attempt <= retries; attempt++)
            {
                try
                {
                    trace("GETNEXT " + (version == CollectorVersion.V1 ? "SNMPv1" : "SNMPv2c") + " at " + oid + " attempt " + (attempt + 1).ToString(CultureInfo.InvariantCulture) + " of " + (retries + 1).ToString(CultureInfo.InvariantCulture) + ".");
                    IList<WalkVariable> response = transport.SendGetNext(version, oid);
                    if (response == null || response.Count == 0)
                    {
                        throw new InvalidOperationException("The QA SNMP library returned an empty response.");
                    }

                    return GetNextResult.Success(response, attempt);
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    trace("GETNEXT " + (version == CollectorVersion.V1 ? "SNMPv1" : "SNMPv2c") + " at " + oid + " attempt failed: " + exception.Message);
                }
            }

            return GetNextResult.Failure("No response for GetNext at OID " + oid + " after " + (retries + 1).ToString(CultureInfo.InvariantCulture) + " attempt(s). Last error: " + (lastException != null ? lastException.Message : "Unknown error."), retries);
        }

        public static List<RootWalkResult> WalkPrefixes(QaSnmpClient initialClient, WalkSettings settings, CollectorVersion version, Action<int, string> trace, Func<QaSnmpClient> clientFactory)
        {
            RootWalkResult[] results = new RootWalkResult[settings.DiscoveryRoots.Length];
            int nextPrefixIndex = 0;
            object queueLock = new object();
            object traceLock = new object();
            CollectionBudget budget = new CollectionBudget(settings.MaximumWalkVariables);
            int workerCount = Math.Min(settings.ConcurrentWalkWorkers, settings.DiscoveryRoots.Length);
            Task[] workers = new Task[workerCount];

            for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
            {
                int currentWorkerIndex = workerIndex;
                workers[workerIndex] = Task.Factory.StartNew(delegate
                {
                    QaSnmpClient workerClient = currentWorkerIndex == 0 ? initialClient : clientFactory();
                    while (true)
                    {
                        int prefixIndex;
                        lock (queueLock)
                        {
                            if (nextPrefixIndex == settings.DiscoveryRoots.Length)
                            {
                                return;
                            }

                            prefixIndex = nextPrefixIndex++;
                        }

                        string prefix = settings.DiscoveryRoots[prefixIndex];
                        if (budget.IsExhausted)
                        {
                            results[prefixIndex] = RootWalkResult.SkippedGlobalSafetyCap(prefix);
                            continue;
                        }

                        try
                        {
                            results[prefixIndex] = workerClient.Walk(version, prefix, settings.Retries, settings.UseGetBulk, settings.BulkMaxRepetitions, budget.TryReserve, delegate (string message)
                            {
                                lock (traceLock)
                                {
                                    trace(3, message);
                                }
                            });
                        }
                        catch (Exception exception)
                        {
                            results[prefixIndex] = new RootWalkResult(prefix, new List<WalkVariable>(), prefix, "request-failed", exception.Message, 0);
                        }
                    }
                });
            }

            Task.WaitAll(workers);
            return new List<RootWalkResult>(results);
        }

        private static bool IsAuthenticationException(Exception exception)
        {
            return exception.GetType().FullName == "SnmpSharpNet.SnmpAuthenticationException";
        }

        private static bool IsEndOfMibValue(string value)
        {
            return String.Equals(value, "SNMP End-of-MIB-View", StringComparison.Ordinal);
        }

        private static void SortVariablesNumerically(IList<WalkVariable> variables)
        {
            List<WalkVariable> list = variables as List<WalkVariable>;
            if (list == null)
            {
                return;
            }

            list.Sort(delegate (WalkVariable left, WalkVariable right)
            {
                int comparison;
                return WalkPrimitives.TryCompareOids(left.Oid, right.Oid, out comparison) ? comparison : StringComparer.Ordinal.Compare(left.Oid, right.Oid);
            });
        }

        internal static string GetPduErrorStatusName(int errorStatus)
        {
            return errorStatus == 1 ? "tooBig" : errorStatus == 5 ? "genErr" : errorStatus == 16 ? "authorizationError" : "SNMP error status " + errorStatus.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class QaSnmpReflectionTransport : ISnmpTransport
    {
        private readonly object client;
        private readonly MethodInfo getMethod;
        private readonly MethodInfo getBulkMethod;
        private readonly MethodInfo getNextMethod;
        private readonly Type pduType;
        private readonly Type pduKindType;
        private readonly Type versionType;

        private QaSnmpReflectionTransport(object client, MethodInfo getMethod, MethodInfo getBulkMethod, MethodInfo getNextMethod, Type pduType, Type pduKindType, Type versionType)
        {
            this.client = client;
            this.getMethod = getMethod;
            this.getBulkMethod = getBulkMethod;
            this.getNextMethod = getNextMethod;
            this.pduType = pduType;
            this.pduKindType = pduKindType;
            this.versionType = versionType;
        }

        public static QaSnmpReflectionTransport Create(string assemblyPath, WalkSettings settings)
        {
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("The DataMiner QA Device Simulator SNMP library was not found.", assemblyPath);
            }

            Assembly assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
            Type simpleSnmpType = assembly.GetType("SnmpSharpNet.SimpleSnmp", true);
            Type pduType = assembly.GetType("SnmpSharpNet.Pdu", true);
            Type pduKindType = assembly.GetType("SnmpSharpNet.PduType", true);
            Type snmpVersionType = assembly.GetType("SnmpSharpNet.SnmpVersion", true);
            MethodInfo getMethod = FindMethod(simpleSnmpType, "Get", snmpVersionType, typeof(string[]));
            MethodInfo getBulkMethod = FindMethod(simpleSnmpType, "GetBulk", pduType);
            MethodInfo getNextMethod = FindMethod(simpleSnmpType, "GetNext", snmpVersionType, typeof(string[]));
            object client = Activator.CreateInstance(simpleSnmpType, new object[] { settings.Address.ToString(), settings.Port, settings.Community, settings.TimeoutMilliseconds, 0 });
            return new QaSnmpReflectionTransport(client, getMethod, getBulkMethod, getNextMethod, pduType, pduKindType, snmpVersionType);
        }

        public IList<WalkVariable> SendGet(CollectorVersion version, string oid)
        {
            return ConvertResponse(Invoke(getMethod, new object[] { GetLibraryVersion(version), new[] { oid } }));
        }

        public IList<WalkVariable> SendGetNext(CollectorVersion version, string oid)
        {
            return ConvertResponse(Invoke(getNextMethod, new object[] { GetLibraryVersion(version), new[] { oid } }));
        }

        public BulkTransportResponse SendGetBulk(string oid, int maxRepetitions)
        {
            object pdu = CreateBulkPdu(oid, maxRepetitions);
            IList<WalkVariable> response = ConvertResponse(Invoke(getBulkMethod, new[] { pdu }));
            int errorStatus = Convert.ToInt32(pduType.GetProperty("ErrorStatus").GetValue(pdu, null), CultureInfo.InvariantCulture);
            return new BulkTransportResponse(response, errorStatus);
        }

        private object CreateBulkPdu(string oid, int maxRepetitions)
        {
            object pdu = Activator.CreateInstance(pduType, new[] { Enum.Parse(pduKindType, "GetBulk") });
            pduType.GetProperty("MaxRepetitions").SetValue(pdu, maxRepetitions, null);
            pduType.GetProperty("NonRepeaters").SetValue(pdu, 0, null);
            object variableBindings = pduType.GetProperty("VbList").GetValue(pdu, null);
            variableBindings.GetType().GetMethod("Add", new[] { typeof(string) }).Invoke(variableBindings, new object[] { oid });
            return pdu;
        }

        private object GetLibraryVersion(CollectorVersion version)
        {
            return Enum.Parse(versionType, version == CollectorVersion.V1 ? "Ver1" : "Ver2");
        }

        private object Invoke(MethodInfo method, object[] parameters)
        {
            try
            {
                return method.Invoke(client, parameters);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static IList<WalkVariable> ConvertResponse(object response)
        {
            IDictionary dictionary = response as IDictionary;
            if (dictionary == null)
            {
                throw new InvalidOperationException("The QA SNMP library returned no response.");
            }

            List<WalkVariable> variables = new List<WalkVariable>();
            foreach (DictionaryEntry entry in dictionary)
            {
                variables.Add(new WalkVariable(
                    Convert.ToString(entry.Key, CultureInfo.InvariantCulture),
                    Convert.ToString(entry.Value, CultureInfo.InvariantCulture)));
            }

            return variables;
        }

        private static MethodInfo FindMethod(Type type, string name, Type parameterType)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == name && parameters.Length == 1 && parameters[0].ParameterType == parameterType)
                {
                    return method;
                }
            }

            throw new MissingMethodException(type.FullName, name);
        }

        private static MethodInfo FindMethod(Type type, string name, Type firstParameterType, Type secondParameterType)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == name && parameters.Length == 2 && parameters[0].ParameterType == firstParameterType && parameters[1].ParameterType == secondParameterType)
                {
                    return method;
                }
            }

            throw new MissingMethodException(type.FullName, name);
        }
    }
}
