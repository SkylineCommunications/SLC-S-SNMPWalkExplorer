namespace SLCSSNMPWalkExplorerApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
    using Skyline.DataMiner.Net.Sections;
    using Skyline.DataMiner.Utils.DOM;
    using Skyline.DataMiner.Utils.DOM.Builders;
    using Skyline.DataMiner.Utils.DOM.Extensions;

    /// <summary>
    /// Stores the non-secret inputs required for an SNMP walk.
    /// </summary>
    public sealed class WalkConfiguration
    {
        /// <summary>Gets or sets the persisted DOM instance identifier.</summary>
        public string Id { get; set; }

        /// <summary>Gets or sets the operator-facing configuration name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the SNMP target address.</summary>
        public string TargetAddress { get; set; }

        /// <summary>Gets or sets the SNMP UDP port.</summary>
        public long TargetPort { get; set; } = 161;

        /// <summary>Gets or sets the credentials-library reference, never credential data.</summary>
        public string CredentialReference { get; set; }

        /// <summary>Gets or sets the per-request timeout in milliseconds.</summary>
        public long TimeoutMilliseconds { get; set; } = 5000;

        /// <summary>Gets or sets the number of SNMP retries.</summary>
        public long Retries { get; set; } = 2;

        /// <summary>Gets or sets the collector log level.</summary>
        public long LogLevel { get; set; } = 1;

        /// <summary>Gets or sets the maximum number of variables to collect.</summary>
        public long MaximumWalkVariables { get; set; } = 100000;

        /// <summary>Gets or sets the concurrent worker limit.</summary>
        public long ConcurrentWalkWorkers { get; set; } = 4;

        /// <summary>Gets or sets whether GETBULK is used.</summary>
        public bool UseGetBulk { get; set; } = true;

        /// <summary>Gets or sets the GETBULK maximum repetitions.</summary>
        public long BulkMaxRepetitions { get; set; } = 25;

        /// <summary>Gets or sets the recommendation binding partition size.</summary>
        public long PartitionRecommendationBindings { get; set; } = 1000;

        /// <summary>Gets or sets the optional diagnostic OID.</summary>
        public string GetBulkDiagnosticOid { get; set; }

        /// <summary>Gets or sets the semicolon-separated discovery roots.</summary>
        public string DiscoveryRoots { get; set; } = "1.3.6.1.1;1.3.6.1.2;1.3.6.1.3;1.3.6.1.4;1.3.6.1.5;1.3.6.1.6;1.3.6.1.7";
    }

    internal sealed class WalkConfigurationRepository
    {
        private readonly IEngine engine;

        internal WalkConfigurationRepository(IEngine engine)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        internal IEnumerable<WalkConfiguration> List()
        {
            DomHelper helper = CreateHelper();
            DomDefinition definition = GetDefinition(helper);
            return helper.DomInstances.ReadAll(definition).Select(ToConfiguration).OrderBy(configuration => configuration.Name).ToList();
        }

        internal WalkConfiguration Create(WalkConfiguration configuration)
        {
            Validate(configuration);

            DomHelper helper = CreateHelper();
            DomDefinition definition = GetDefinition(helper);
            DomInstance instance = new DomInstanceBuilder(definition)
                .WithID(Guid.NewGuid())
                .AddSection(new SectionDefinitionID(SnmpWalkConfigurationSchema.SectionId), section => section
                    .WithFieldValue(SnmpWalkConfigurationSchema.NameFieldId, configuration.Name.Trim())
                    .WithFieldValue(SnmpWalkConfigurationSchema.TargetAddressFieldId, configuration.TargetAddress.Trim())
                    .WithFieldValue(SnmpWalkConfigurationSchema.TargetPortFieldId, configuration.TargetPort)
                    .WithFieldValue(SnmpWalkConfigurationSchema.CredentialReferenceFieldId, configuration.CredentialReference.Trim())
                    .WithFieldValue(SnmpWalkConfigurationSchema.TimeoutMillisecondsFieldId, configuration.TimeoutMilliseconds)
                    .WithFieldValue(SnmpWalkConfigurationSchema.RetriesFieldId, configuration.Retries)
                    .WithFieldValue(SnmpWalkConfigurationSchema.LogLevelFieldId, configuration.LogLevel)
                    .WithFieldValue(SnmpWalkConfigurationSchema.MaximumWalkVariablesFieldId, configuration.MaximumWalkVariables)
                    .WithFieldValue(SnmpWalkConfigurationSchema.ConcurrentWalkWorkersFieldId, configuration.ConcurrentWalkWorkers)
                    .WithFieldValue(SnmpWalkConfigurationSchema.UseGetBulkFieldId, configuration.UseGetBulk)
                    .WithFieldValue(SnmpWalkConfigurationSchema.BulkMaxRepetitionsFieldId, configuration.BulkMaxRepetitions)
                    .WithFieldValue(SnmpWalkConfigurationSchema.PartitionRecommendationBindingsFieldId, configuration.PartitionRecommendationBindings)
                    .WithFieldValue(SnmpWalkConfigurationSchema.GetBulkDiagnosticOidFieldId, configuration.GetBulkDiagnosticOid)
                    .WithFieldValue(SnmpWalkConfigurationSchema.DiscoveryRootsFieldId, configuration.DiscoveryRoots.Trim()))
                .Build();

            DomInstance created = helper.DomInstances.Create(instance);
            return ToConfiguration(created);
        }

        private DomHelper CreateHelper()
        {
            return new DomHelper(engine.SendSLNetMessages, SnmpWalkConfigurationSchema.ModuleId);
        }

        private static DomDefinition GetDefinition(DomHelper helper)
        {
            DomDefinition definition = helper.DomDefinitions.GetByID(SnmpWalkConfigurationSchema.DefinitionId);
            if (definition == null)
            {
                throw new InvalidOperationException("The SNMP Walk configuration schema has not been provisioned.");
            }

            return definition;
        }

        private static WalkConfiguration ToConfiguration(DomInstance instance)
        {
            Section section = instance.GetSectionsWithDefinition(new SectionDefinitionID(SnmpWalkConfigurationSchema.SectionId)).Single();
            return new WalkConfiguration
            {
                Id = instance.ID.Id.ToString(),
                Name = section.GetFieldValue<string>(SnmpWalkConfigurationSchema.NameFieldId),
                TargetAddress = section.GetFieldValue<string>(SnmpWalkConfigurationSchema.TargetAddressFieldId),
                TargetPort = section.GetFieldValue<long>(SnmpWalkConfigurationSchema.TargetPortFieldId),
                CredentialReference = section.GetFieldValue<string>(SnmpWalkConfigurationSchema.CredentialReferenceFieldId),
                TimeoutMilliseconds = section.GetFieldValue<long>(SnmpWalkConfigurationSchema.TimeoutMillisecondsFieldId),
                Retries = section.GetFieldValue<long>(SnmpWalkConfigurationSchema.RetriesFieldId),
                LogLevel = section.GetFieldValue<long>(SnmpWalkConfigurationSchema.LogLevelFieldId),
                MaximumWalkVariables = section.GetFieldValue<long>(SnmpWalkConfigurationSchema.MaximumWalkVariablesFieldId),
                ConcurrentWalkWorkers = section.GetFieldValue<long>(SnmpWalkConfigurationSchema.ConcurrentWalkWorkersFieldId),
                UseGetBulk = section.GetFieldValue<bool>(SnmpWalkConfigurationSchema.UseGetBulkFieldId),
                BulkMaxRepetitions = section.GetFieldValue<long>(SnmpWalkConfigurationSchema.BulkMaxRepetitionsFieldId),
                PartitionRecommendationBindings = section.GetFieldValue<long>(SnmpWalkConfigurationSchema.PartitionRecommendationBindingsFieldId),
                GetBulkDiagnosticOid = section.GetFieldValue<string>(SnmpWalkConfigurationSchema.GetBulkDiagnosticOidFieldId),
                DiscoveryRoots = section.GetFieldValue<string>(SnmpWalkConfigurationSchema.DiscoveryRootsFieldId),
            };
        }

        private static void Validate(WalkConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (String.IsNullOrWhiteSpace(configuration.Name) || String.IsNullOrWhiteSpace(configuration.TargetAddress) || String.IsNullOrWhiteSpace(configuration.CredentialReference))
            {
                throw new ArgumentException("Name, target address, and credential reference are required.");
            }

            if (configuration.TargetPort < 1 || configuration.TargetPort > 65535 || configuration.TimeoutMilliseconds < 1 || configuration.Retries < 0 || configuration.MaximumWalkVariables < 1 || configuration.ConcurrentWalkWorkers < 1 || configuration.BulkMaxRepetitions < 1 || configuration.PartitionRecommendationBindings < 1 || String.IsNullOrWhiteSpace(configuration.DiscoveryRoots))
            {
                throw new ArgumentException("The configuration contains an invalid numeric value or no discovery roots.");
            }
        }
    }
}