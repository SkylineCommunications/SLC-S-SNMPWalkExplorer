namespace SLCSSNMPWalkExplorerApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
    using Skyline.DataMiner.Net.Apps.Modules;
    using Skyline.DataMiner.Net.Apps.Sections.SectionDefinitions;
    using Skyline.DataMiner.Net.Messages.SLDataGateway;
    using Skyline.DataMiner.Net.Sections;
    using Skyline.DataMiner.Utils.DOM.Extensions;

    internal static class SnmpWalkConfigurationSchema
    {
        internal const string ModuleId = "snmpwalkexplorer";
        internal static readonly Guid SectionId = Guid.Parse("494dd5cb-fb50-4df8-a1ad-bfa0b9977b01");
        internal static readonly Guid DefinitionId = Guid.Parse("3a721b31-07d5-4248-9c8f-7e63847c9e26");
        internal static readonly FieldDescriptorID NameFieldId = new FieldDescriptorID(Guid.Parse("493ab98c-7792-4e03-9604-131af303e821"));
        internal static readonly FieldDescriptorID TargetAddressFieldId = new FieldDescriptorID(Guid.Parse("0993e1ba-3f35-4bce-988d-3d317defc0c7"));
        internal static readonly FieldDescriptorID TargetPortFieldId = new FieldDescriptorID(Guid.Parse("34cacd69-f0a1-44b2-9c7d-67629378b1f8"));
        internal static readonly FieldDescriptorID CredentialReferenceFieldId = new FieldDescriptorID(Guid.Parse("7229cbe2-414b-4f1f-9e90-9026b486ad59"));
        internal static readonly FieldDescriptorID TimeoutMillisecondsFieldId = new FieldDescriptorID(Guid.Parse("1f7d9c87-5f03-4067-9401-cd108792ba36"));
        internal static readonly FieldDescriptorID RetriesFieldId = new FieldDescriptorID(Guid.Parse("fb6cc76d-1c0b-45e4-ae96-4a4c618494d7"));
        internal static readonly FieldDescriptorID LogLevelFieldId = new FieldDescriptorID(Guid.Parse("90ea3eb7-ff30-4d3b-ab22-f2b7bd2b73e5"));
        internal static readonly FieldDescriptorID MaximumWalkVariablesFieldId = new FieldDescriptorID(Guid.Parse("a3e43e02-48fe-4dca-97db-bf8e91794c4d"));
        internal static readonly FieldDescriptorID ConcurrentWalkWorkersFieldId = new FieldDescriptorID(Guid.Parse("c184b5fe-24a2-49be-b0f8-9e273058c6c4"));
        internal static readonly FieldDescriptorID UseGetBulkFieldId = new FieldDescriptorID(Guid.Parse("5cbebd72-265d-40c9-bcff-3a9044ae8392"));
        internal static readonly FieldDescriptorID BulkMaxRepetitionsFieldId = new FieldDescriptorID(Guid.Parse("ae0b7c51-81a9-4ab9-b14a-74974008197f"));
        internal static readonly FieldDescriptorID PartitionRecommendationBindingsFieldId = new FieldDescriptorID(Guid.Parse("b0426c64-ccca-492f-adbf-7f132f55e807"));
        internal static readonly FieldDescriptorID GetBulkDiagnosticOidFieldId = new FieldDescriptorID(Guid.Parse("fda17ce9-1f43-4027-bb39-f68e6c0f4776"));
        internal static readonly FieldDescriptorID DiscoveryRootsFieldId = new FieldDescriptorID(Guid.Parse("fe8e0556-5fae-4ea0-8c6e-194ab4b990c3"));

        internal static void EnsureProvisioned(IEngine engine)
        {
            ModuleSettingsHelper moduleSettingsHelper = new ModuleSettingsHelper(engine.SendSLNetMessages);
            ModuleSettings moduleSettings = moduleSettingsHelper.ModuleSettings.Read(ModuleSettingsExposers.ModuleId.Equal(ModuleId)).FirstOrDefault();
            if (moduleSettings == null)
            {
                moduleSettingsHelper.ModuleSettings.Create(new ModuleSettings(ModuleId));
            }

            DomHelper helper = new DomHelper(engine.SendSLNetMessages, ModuleId);
            CustomSectionDefinition sectionDefinition = helper.SectionDefinitions.GetByID(SectionId) as CustomSectionDefinition;
            if (sectionDefinition == null)
            {
                sectionDefinition = new CustomSectionDefinition
                {
                    ID = new SectionDefinitionID(SectionId),
                    Name = "SNMP Walk Configuration",
                };

                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(NameFieldId, "Name", typeof(string), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(TargetAddressFieldId, "TargetAddress", typeof(string), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(TargetPortFieldId, "TargetPort", typeof(long), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(CredentialReferenceFieldId, "CredentialReference", typeof(string), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(TimeoutMillisecondsFieldId, "TimeoutMilliseconds", typeof(long), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(RetriesFieldId, "Retries", typeof(long), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(LogLevelFieldId, "LogLevel", typeof(long), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(MaximumWalkVariablesFieldId, "MaximumWalkVariables", typeof(long), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(ConcurrentWalkWorkersFieldId, "ConcurrentWalkWorkers", typeof(long), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(UseGetBulkFieldId, "UseGetBulk", typeof(bool), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(BulkMaxRepetitionsFieldId, "BulkMaxRepetitions", typeof(long), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(PartitionRecommendationBindingsFieldId, "PartitionRecommendationBindings", typeof(long), false));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(GetBulkDiagnosticOidFieldId, "GetBulkDiagnosticOid", typeof(string), true));
                sectionDefinition.AddOrReplaceFieldDescriptor(CreateField(DiscoveryRootsFieldId, "DiscoveryRoots", typeof(string), false));
                sectionDefinition = helper.SectionDefinitions.Create(sectionDefinition) as CustomSectionDefinition;
            }

            DomDefinition definition = helper.DomDefinitions.GetByID(DefinitionId);
            if (definition == null)
            {
                helper.DomDefinitions.Create(new DomDefinition
                {
                    ID = new DomDefinitionId(DefinitionId),
                    Name = "SNMP Walk Configuration",
                    SectionDefinitionLinks = new List<SectionDefinitionLink>
                    {
                        new SectionDefinitionLink(sectionDefinition.GetID()),
                    },
                });
            }
        }

        private static FieldDescriptor CreateField(FieldDescriptorID id, string name, Type type, bool isOptional)
        {
            return new FieldDescriptor
            {
            ID = id,
                Name = name,
                FieldType = type,
                IsOptional = isOptional,
            };
        }
    }
}