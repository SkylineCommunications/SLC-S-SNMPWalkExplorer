using System;
using System.Collections.Generic;
using System.Globalization;

namespace SLCSSNMPWalkCollector
{
    internal sealed class WalkRunSettingsSnapshot
    {
        private static readonly string[] DefaultDiscoveryRoots =
        {
            "1.3.6.1.1",
            "1.3.6.1.2",
            "1.3.6.1.3",
            "1.3.6.1.4",
            "1.3.6.1.5",
            "1.3.6.1.6",
            "1.3.6.1.7",
        };

        private WalkRunSettingsSnapshot(
            WalkConnectionConfiguration connection,
            int timeoutMilliseconds,
            int retries,
            int logLevel,
            int maximumWalkVariables,
            int concurrentWalkWorkers,
            bool useGetBulk,
            int bulkMaxRepetitions,
            int partitionRecommendationBindings,
            string getBulkDiagnosticOid,
            string[] discoveryRoots,
            string runCorrelationId)
        {
            Connection = connection;
            TimeoutMilliseconds = timeoutMilliseconds;
            Retries = retries;
            LogLevel = logLevel;
            MaximumWalkVariables = maximumWalkVariables;
            ConcurrentWalkWorkers = concurrentWalkWorkers;
            UseGetBulk = useGetBulk;
            BulkMaxRepetitions = bulkMaxRepetitions;
            PartitionRecommendationBindings = partitionRecommendationBindings;
            GetBulkDiagnosticOid = getBulkDiagnosticOid;
            DiscoveryRoots = discoveryRoots;
            RunCorrelationId = runCorrelationId;
        }

        public int BulkMaxRepetitions { get; private set; }

        public int ConcurrentWalkWorkers { get; private set; }

        public WalkConnectionConfiguration Connection { get; private set; }

        public string[] DiscoveryRoots { get; private set; }

        public string GetBulkDiagnosticOid { get; private set; }

        public int LogLevel { get; private set; }

        public int MaximumWalkVariables { get; private set; }

        public int PartitionRecommendationBindings { get; private set; }

        public int Retries { get; private set; }

        public string RunCorrelationId { get; private set; }

        public int TimeoutMilliseconds { get; private set; }

        public bool UseGetBulk { get; private set; }

        public static WalkRunSettingsSnapshot Create(
            WalkConnectionConfiguration connection,
            int timeoutMilliseconds,
            int retries,
            int logLevel,
            int maximumWalkVariables,
            int concurrentWalkWorkers,
            bool useGetBulk,
            int bulkMaxRepetitions,
            int partitionRecommendationBindings,
            string getBulkDiagnosticOid,
            string discoveryRoots,
            string runCorrelationId)
        {
            if (connection == null)
            {
                throw new ArgumentNullException("connection");
            }

            if (timeoutMilliseconds < 1 || timeoutMilliseconds > 60000)
            {
                throw new ArgumentOutOfRangeException("timeoutMilliseconds", "TimeoutMilliseconds must be between 1 and 60000.");
            }

            if (retries < 0 || retries > 10)
            {
                throw new ArgumentOutOfRangeException("retries", "Retries must be between 0 and 10.");
            }

            if (logLevel < 0 || logLevel > 3)
            {
                throw new ArgumentOutOfRangeException("logLevel", "LogLevel must be between 0 and 3.");
            }

            if (maximumWalkVariables < 1)
            {
                throw new ArgumentOutOfRangeException("maximumWalkVariables", "MaximumWalkVariables must be at least 1.");
            }

            if (concurrentWalkWorkers < 1 || concurrentWalkWorkers > 64)
            {
                throw new ArgumentOutOfRangeException("concurrentWalkWorkers", "ConcurrentWalkWorkers must be between 1 and 64.");
            }

            if (bulkMaxRepetitions < 1 || bulkMaxRepetitions > 100)
            {
                throw new ArgumentOutOfRangeException("bulkMaxRepetitions", "BulkMaxRepetitions must be between 1 and 100.");
            }

            if (partitionRecommendationBindings < 1)
            {
                throw new ArgumentOutOfRangeException("partitionRecommendationBindings", "PartitionRecommendationBindings must be at least 1.");
            }

            if (!String.IsNullOrWhiteSpace(getBulkDiagnosticOid) && !WalkPrimitives.IsValidOid(getBulkDiagnosticOid))
            {
                throw new ArgumentException("GetBulkDiagnosticOid must be empty or a numeric OID.", "getBulkDiagnosticOid");
            }

            return new WalkRunSettingsSnapshot(
                connection,
                timeoutMilliseconds,
                retries,
                logLevel,
                maximumWalkVariables,
                concurrentWalkWorkers,
                useGetBulk,
                bulkMaxRepetitions,
                partitionRecommendationBindings,
                getBulkDiagnosticOid ?? String.Empty,
                ParseDiscoveryRoots(discoveryRoots),
                runCorrelationId ?? String.Empty);
        }

        public static WalkRunSettingsSnapshot CreateFromParameterValues(
            string targetAddress,
            string targetPort,
            string community,
            string timeoutMilliseconds,
            string retries,
            string logLevel,
            string maximumWalkVariables,
            string concurrentWalkWorkers,
            string useGetBulk,
            string bulkMaxRepetitions,
            string partitionRecommendationBindings,
            string getBulkDiagnosticOid,
            string discoveryRoots,
            string runCorrelationId)
        {
            return Create(
                WalkConnectionConfiguration.Create(targetAddress, ParseInteger(targetPort, "TargetPort"), community),
                ParseInteger(timeoutMilliseconds, "TimeoutMilliseconds"),
                ParseInteger(retries, "Retries"),
                ParseInteger(logLevel, "LogLevel"),
                ParseInteger(maximumWalkVariables, "MaximumWalkVariables"),
                ParseInteger(concurrentWalkWorkers, "ConcurrentWalkWorkers"),
                ParseBoolean(useGetBulk, "UseGetBulk"),
                ParseInteger(bulkMaxRepetitions, "BulkMaxRepetitions"),
                ParseInteger(partitionRecommendationBindings, "PartitionRecommendationBindings"),
                getBulkDiagnosticOid,
                discoveryRoots,
                runCorrelationId);
        }

        private static string[] ParseDiscoveryRoots(string value)
        {
            string[] suppliedRoots = String.IsNullOrWhiteSpace(value)
                ? DefaultDiscoveryRoots
                : value.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> roots = new List<string>();

            foreach (string suppliedRoot in suppliedRoots)
            {
                string root = suppliedRoot.Trim();
                if (!WalkPrimitives.IsValidOid(root))
                {
                    throw new ArgumentException("DiscoveryRoots must contain only numeric OIDs.", "DiscoveryRoots");
                }

                foreach (string existingRoot in roots)
                {
                    if (String.Equals(root, existingRoot, StringComparison.Ordinal) ||
                        root.StartsWith(existingRoot + ".", StringComparison.Ordinal) ||
                        existingRoot.StartsWith(root + ".", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("DiscoveryRoots must be distinct and non-overlapping.", "DiscoveryRoots");
                    }
                }

                roots.Add(root);
            }

            if (roots.Count == 0)
            {
                throw new ArgumentException("DiscoveryRoots must contain at least one numeric OID.", "DiscoveryRoots");
            }

            return roots.ToArray();
        }

        private static bool ParseBoolean(string value, string parameterName)
        {
            bool parsedValue;
            if (!Boolean.TryParse(value, out parsedValue))
            {
                throw new ArgumentException(parameterName + " must be either true or false.", parameterName);
            }

            return parsedValue;
        }

        private static int ParseInteger(string value, string parameterName)
        {
            int parsedValue;
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
            {
                throw new ArgumentException(parameterName + " must be an integer.", parameterName);
            }

            return parsedValue;
        }
    }
}