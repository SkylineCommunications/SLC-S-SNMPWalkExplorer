namespace SLCSSNMPWalkExplorerApi
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using Skyline.DataMiner.Utils.SecureCoding.SecureIO;

    internal sealed class WalkArtifactCatalog
    {
        internal const string DefaultArtifactDirectory = @"C:\Skyline DataMiner\Documents\SLC-S-SNMPWalkCollector";
        private const long MaximumRawArtifactBytes = 16L * 1024L * 1024L;
        private const int MaximumBindingSearchResults = 1000;
        private const int MaximumTreeBindings = 1000;
        private const string MetadataSearchPattern = "*.walk.metadata.json";

        private readonly SecurePath artifactDirectory;

        public WalkArtifactCatalog(string artifactDirectory)
        {
            if (String.IsNullOrWhiteSpace(artifactDirectory))
            {
                throw new ArgumentException("An artifact directory is required.", "artifactDirectory");
            }

            this.artifactDirectory = SecurePath.CreateSecurePath(artifactDirectory);
        }

        public IList<WalkArtifact> ListArtifacts()
        {
            List<WalkArtifact> artifacts = new List<WalkArtifact>();
            if (!Directory.Exists(artifactDirectory))
            {
                return artifacts;
            }

            foreach (string metadataPath in Directory.EnumerateFiles(artifactDirectory, MetadataSearchPattern, SearchOption.TopDirectoryOnly))
            {
                WalkArtifact artifact;
                if (TryReadCommittedArtifact(SecurePath.CreateSecurePath(metadataPath), out artifact))
                {
                    artifacts.Add(artifact);
                }
            }

            artifacts.Sort(delegate (WalkArtifact left, WalkArtifact right)
            {
                return StringComparer.Ordinal.Compare(right.CompletedAtUtc, left.CompletedAtUtc);
            });

            return artifacts;
        }

        public string SerializeArtifacts()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('{').Append("\"artifacts\":[");
            IList<WalkArtifact> artifacts = ListArtifacts();
            for (int index = 0; index < artifacts.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendArtifact(builder, artifacts[index]);
            }

            return builder.Append("]}").ToString();
        }

        public bool TryReadRawArtifact(string artifactId, out string rawArtifact)
        {
            rawArtifact = null;
            if (String.IsNullOrWhiteSpace(artifactId))
            {
                return false;
            }

            foreach (WalkArtifact artifact in ListArtifacts())
            {
                if (!String.Equals(artifact.Id, artifactId, StringComparison.Ordinal))
                {
                    continue;
                }

                SecurePath rawPath = SecurePath.ConstructSecurePath(artifactDirectory, artifact.RawFileName);
                FileInfo rawFile = new FileInfo(rawPath);
                if (!rawFile.Exists || rawFile.Length > MaximumRawArtifactBytes)
                {
                    return false;
                }

                try
                {
                    rawArtifact = File.ReadAllText(rawPath, Encoding.UTF8);
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
            }

            return false;
        }

        public bool TryDeleteArtifact(string artifactId)
        {
            if (String.IsNullOrWhiteSpace(artifactId))
            {
                return false;
            }

            foreach (WalkArtifact artifact in ListArtifacts())
            {
                if (!String.Equals(artifact.Id, artifactId, StringComparison.Ordinal))
                {
                    continue;
                }

                SecurePath rawPath = SecurePath.ConstructSecurePath(artifactDirectory, artifact.RawFileName);
                SecurePath metadataPath = SecurePath.ConstructSecurePath(artifactDirectory, artifact.RawFileName + ".metadata.json");

                bool deletedAny = false;
                if (File.Exists(rawPath))
                {
                    File.Delete(rawPath);
                    deletedAny = true;
                }

                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                    deletedAny = true;
                }

                return deletedAny;
            }

            return false;
        }

        public bool TrySearchBindings(string artifactId, string oidPrefix, string valueContains, out string bindings)
        {
            bindings = null;
            if (!String.IsNullOrWhiteSpace(oidPrefix) && !IsValidNumericOid(oidPrefix))
            {
                throw new ArgumentException("The OID prefix must be numeric.", "oidPrefix");
            }

            string rawArtifact;
            if (!TryReadRawArtifact(artifactId, out rawArtifact))
            {
                return false;
            }

            List<RawBinding> matches = new List<RawBinding>();
            using (StringReader reader = new StringReader(rawArtifact))
            {
                string line;
                while ((line = reader.ReadLine()) != null && matches.Count < MaximumBindingSearchResults)
                {
                    RawBinding binding = DeserializeBinding(line);
                    if (binding == null || !Matches(binding, oidPrefix, valueContains))
                    {
                        continue;
                    }

                    matches.Add(binding);
                }
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('{').Append("\"bindings\":[");
            for (int index = 0; index < matches.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append('{');
                AppendString(builder, "oid", matches[index].Oid, true);
                AppendString(builder, "value", matches[index].Value, false);
                builder.Append('}');
            }

            bindings = builder.Append("]}").ToString();
            return true;
        }

        public bool TryBuildTree(string artifactId, out string tree)
        {
            tree = null;
            SecurePath rawPath;
            if (!TryGetCommittedRawArtifactPath(artifactId, out rawPath))
            {
                return false;
            }

            TreeNode root = new TreeNode("root", "Observed OIDs");
            try
            {
                using (StreamReader reader = new StreamReader(rawPath, Encoding.UTF8))
                {
                    string line;
                    while (root.Bindings < MaximumTreeBindings && (line = reader.ReadLine()) != null)
                    {
                        RawBinding binding = DeserializeBinding(line);
                        if (binding == null || !IsValidNumericOid(binding.Oid))
                        {
                            continue;
                        }

                        AddBinding(root, binding);
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }

            StringBuilder builder = new StringBuilder();
            AppendTreeNode(builder, root);
            tree = builder.ToString();
            return true;
        }

        private static RawBinding DeserializeBinding(string line)
        {
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(RawBinding));
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(line)))
                {
                    return serializer.ReadObject(stream) as RawBinding;
                }
            }
            catch (SerializationException)
            {
                return null;
            }
        }

        private static bool Matches(RawBinding binding, string oidPrefix, string valueContains)
        {
            bool matchesOid = String.IsNullOrWhiteSpace(oidPrefix) ||
                String.Equals(binding.Oid, oidPrefix, StringComparison.Ordinal) ||
                binding.Oid.StartsWith(oidPrefix + ".", StringComparison.Ordinal);
            bool matchesValue = String.IsNullOrWhiteSpace(valueContains) ||
                (!String.IsNullOrEmpty(binding.Value) && binding.Value.IndexOf(valueContains, StringComparison.OrdinalIgnoreCase) >= 0);

            return matchesOid && matchesValue;
        }

        private static bool IsValidNumericOid(string oid)
        {
            if (String.IsNullOrWhiteSpace(oid))
            {
                return false;
            }

            foreach (string arc in oid.Split('.'))
            {
                ulong parsedArc;
                if (!UInt64.TryParse(arc, NumberStyles.None, CultureInfo.InvariantCulture, out parsedArc))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryGetCommittedRawArtifactPath(string artifactId, out SecurePath rawPath)
        {
            rawPath = null;
            if (String.IsNullOrWhiteSpace(artifactId))
            {
                return false;
            }

            foreach (WalkArtifact artifact in ListArtifacts())
            {
                if (!String.Equals(artifact.Id, artifactId, StringComparison.Ordinal))
                {
                    continue;
                }

                SecurePath candidatePath = SecurePath.ConstructSecurePath(artifactDirectory, artifact.RawFileName);
                FileInfo rawFile = new FileInfo(candidatePath);
                if (!rawFile.Exists || rawFile.Length > MaximumRawArtifactBytes)
                {
                    return false;
                }

                rawPath = candidatePath;
                return true;
            }

            return false;
        }

        private static void AddBinding(TreeNode root, RawBinding binding)
        {
            root.Bindings++;
            TreeNode current = root;
            foreach (string arc in binding.Oid.Split('.'))
            {
                current = current.GetOrAddChild(arc);
                current.Bindings++;
            }

            current.Value = binding.Value;
        }

        private static void AppendTreeNode(StringBuilder builder, TreeNode node)
        {
            builder.Append('{');
            AppendString(builder, "oid", node.Oid, true);
            AppendString(builder, "label", node.Label, true);
            AppendNumber(builder, "bindings", node.Bindings, node.Children.Count > 0 || node.Value != null);
            if (node.Children.Count > 0)
            {
                builder.Append("\"children\":[");
                node.Children.Sort(CompareTreeNodes);
                for (int index = 0; index < node.Children.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    AppendTreeNode(builder, node.Children[index]);
                }

                builder.Append(']');
            }
            else if (node.Value != null)
            {
                AppendString(builder, "value", node.Value, false);
            }

            builder.Append('}');
        }

        private static int CompareTreeNodes(TreeNode left, TreeNode right)
        {
            ulong leftArc;
            ulong rightArc;
            UInt64.TryParse(left.Label, NumberStyles.None, CultureInfo.InvariantCulture, out leftArc);
            UInt64.TryParse(right.Label, NumberStyles.None, CultureInfo.InvariantCulture, out rightArc);
            return leftArc.CompareTo(rightArc);
        }

        private bool TryReadCommittedArtifact(SecurePath metadataPath, out WalkArtifact artifact)
        {
            artifact = null;
            try
            {
                MetadataDocument metadata = ReadMetadata(metadataPath);
                if (metadata == null || !IsSafeRawFileName(metadata.RawFileName))
                {
                    return false;
                }

                SecurePath rawPath = SecurePath.ConstructSecurePath(artifactDirectory, metadata.RawFileName);
                if (!File.Exists(rawPath) || !String.Equals(Path.GetFileName(metadataPath), metadata.RawFileName + ".metadata.json", StringComparison.Ordinal))
                {
                    return false;
                }

                artifact = new WalkArtifact(metadata);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (SerializationException)
            {
                return false;
            }
        }

        private static MetadataDocument ReadMetadata(SecurePath metadataPath)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(MetadataDocument));
            using (FileStream stream = File.OpenRead(metadataPath))
            {
                return (MetadataDocument)serializer.ReadObject(stream);
            }
        }

        private static bool IsSafeRawFileName(string rawFileName)
        {
            return !String.IsNullOrWhiteSpace(rawFileName) &&
                String.Equals(rawFileName, Path.GetFileName(rawFileName), StringComparison.Ordinal) &&
                rawFileName.EndsWith(".walk", StringComparison.Ordinal) &&
                rawFileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static void AppendArtifact(StringBuilder builder, WalkArtifact artifact)
        {
            builder.Append('{');
            AppendString(builder, "id", artifact.Id, true);
            AppendString(builder, "rawFileName", artifact.RawFileName, true);
            AppendString(builder, "startedAtUtc", artifact.StartedAtUtc, true);
            AppendString(builder, "completedAtUtc", artifact.CompletedAtUtc, true);
            AppendString(builder, "targetAddress", artifact.TargetAddress, true);
            AppendNumber(builder, "targetPort", artifact.TargetPort, true);
            AppendString(builder, "snmpVersion", artifact.SnmpVersion, true);
            AppendNumber(builder, "concurrentWalkWorkers", artifact.ConcurrentWalkWorkers, true);
            AppendBoolean(builder, "useGetBulk", artifact.UseGetBulk, true);
            AppendNumber(builder, "totalBindings", artifact.TotalBindings, true);
            AppendBoolean(builder, "isComplete", artifact.IsComplete, true);
            builder.Append("\"rootOutcomes\":[");
            for (int index = 0; index < artifact.RootOutcomes.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                RootOutcome outcome = artifact.RootOutcomes[index];
                builder.Append('{');
                AppendString(builder, "rootOid", outcome.RootOid, true);
                AppendNumber(builder, "bindingCount", outcome.BindingCount, true);
                AppendString(builder, "lastOid", outcome.LastOid, true);
                AppendString(builder, "terminalState", outcome.TerminalState, true);
                AppendBoolean(builder, "isComplete", outcome.IsComplete, true);
                AppendBoolean(builder, "partitionRecommended", outcome.PartitionRecommended, true);
                AppendNumber(builder, "retryCount", outcome.RetryCount, !String.IsNullOrEmpty(outcome.Error));
                if (!String.IsNullOrEmpty(outcome.Error))
                {
                    AppendString(builder, "error", outcome.Error, false);
                }

                builder.Append('}');
            }

            builder.Append("]}");
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool appendComma)
        {
            builder.Append('"').Append(name).Append("\":");
            if (value == null)
            {
                builder.Append("null");
            }
            else
            {
                builder.Append('"').Append(EscapeJson(value)).Append('"');
            }

            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendNumber(StringBuilder builder, string name, int value, bool appendComma)
        {
            builder.Append('"').Append(name).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendBoolean(StringBuilder builder, string name, bool value, bool appendComma)
        {
            builder.Append('"').Append(name).Append("\":").Append(value ? "true" : "false");
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static string EscapeJson(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length + 8);
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            return builder.ToString();
        }

        [DataContract]
        internal sealed class MetadataDocument
        {
            [DataMember(Name = "rawFileName")]
            public string RawFileName { get; set; }

            [DataMember(Name = "startedAtUtc")]
            public string StartedAtUtc { get; set; }

            [DataMember(Name = "completedAtUtc")]
            public string CompletedAtUtc { get; set; }

            [DataMember(Name = "targetAddress")]
            public string TargetAddress { get; set; }

            [DataMember(Name = "targetPort")]
            public int TargetPort { get; set; }

            [DataMember(Name = "snmpVersion")]
            public string SnmpVersion { get; set; }

            [DataMember(Name = "concurrentWalkWorkers")]
            public int ConcurrentWalkWorkers { get; set; }

            [DataMember(Name = "useGetBulk")]
            public bool UseGetBulk { get; set; }

            [DataMember(Name = "totalBindings")]
            public int TotalBindings { get; set; }

            [DataMember(Name = "isComplete")]
            public bool IsComplete { get; set; }

            [DataMember(Name = "rootOutcomes")]
            public List<RootOutcome> RootOutcomes { get; set; }
        }

        [DataContract]
        private sealed class RawBinding
        {
            [DataMember(Name = "oid")]
            public string Oid { get; set; }

            [DataMember(Name = "value")]
            public string Value { get; set; }
        }

        private sealed class TreeNode
        {
            public TreeNode(string oid, string label)
            {
                Oid = oid;
                Label = label;
                Children = new List<TreeNode>();
            }

            public string Oid { get; private set; }
            public string Label { get; private set; }
            public int Bindings { get; set; }
            public string Value { get; set; }
            public List<TreeNode> Children { get; private set; }

            public TreeNode GetOrAddChild(string arc)
            {
                foreach (TreeNode child in Children)
                {
                    if (String.Equals(child.Label, arc, StringComparison.Ordinal))
                    {
                        return child;
                    }
                }

                string oid = Oid == "root" ? arc : Oid + "." + arc;
                TreeNode addedChild = new TreeNode(oid, arc);
                Children.Add(addedChild);
                return addedChild;
            }
        }

        [DataContract]
        internal sealed class RootOutcome
        {
            [DataMember(Name = "rootOid")]
            public string RootOid { get; set; }

            [DataMember(Name = "bindingCount")]
            public int BindingCount { get; set; }

            [DataMember(Name = "lastOid")]
            public string LastOid { get; set; }

            [DataMember(Name = "terminalState")]
            public string TerminalState { get; set; }

            [DataMember(Name = "isComplete")]
            public bool IsComplete { get; set; }

            [DataMember(Name = "partitionRecommended")]
            public bool PartitionRecommended { get; set; }

            [DataMember(Name = "retryCount")]
            public int RetryCount { get; set; }

            [DataMember(Name = "error")]
            public string Error { get; set; }
        }

        internal sealed class WalkArtifact
        {
            internal WalkArtifact(MetadataDocument metadata)
            {
                RawFileName = metadata.RawFileName;
                Id = Path.GetFileNameWithoutExtension(RawFileName);
                StartedAtUtc = metadata.StartedAtUtc;
                CompletedAtUtc = metadata.CompletedAtUtc;
                TargetAddress = metadata.TargetAddress;
                TargetPort = metadata.TargetPort;
                SnmpVersion = metadata.SnmpVersion;
                ConcurrentWalkWorkers = metadata.ConcurrentWalkWorkers;
                UseGetBulk = metadata.UseGetBulk;
                TotalBindings = metadata.TotalBindings;
                IsComplete = metadata.IsComplete;
                RootOutcomes = metadata.RootOutcomes ?? new List<RootOutcome>();
            }

            public string Id { get; private set; }
            public string RawFileName { get; private set; }
            public string StartedAtUtc { get; private set; }
            public string CompletedAtUtc { get; private set; }
            public string TargetAddress { get; private set; }
            public int TargetPort { get; private set; }
            public string SnmpVersion { get; private set; }
            public int ConcurrentWalkWorkers { get; private set; }
            public bool UseGetBulk { get; private set; }
            public int TotalBindings { get; private set; }
            public bool IsComplete { get; private set; }
            public IList<RootOutcome> RootOutcomes { get; private set; }
        }
    }
}