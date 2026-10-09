namespace SLCSSNMPWalkExplorerApi
{
    using System;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;

    internal sealed class DomModuleAccess
    {
        private readonly IEngine engine;
        private readonly string moduleId;

        public DomModuleAccess(IEngine engine, string moduleId)
        {
            if (engine == null)
            {
                throw new ArgumentNullException("engine");
            }

            if (String.IsNullOrWhiteSpace(moduleId))
            {
                throw new ArgumentException("A provisioned DOM module ID is required.", "moduleId");
            }

            this.engine = engine;
            this.moduleId = moduleId;
        }

        public DomHelper CreateHelper()
        {
            return new DomHelper(engine.SendSLNetMessages, moduleId);
        }
    }
}