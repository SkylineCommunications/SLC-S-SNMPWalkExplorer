using System;
using System.IO;

using Skyline.AppInstaller;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.AppPackages;
using Skyline.DataMiner.Utils.SecureCoding.SecureIO;
using Skyline.DataMiner.Utils.UserDefinedApiToolkit.Installer;
using SLCSSNMPWalkExplorerApi;

/// <summary>
/// DataMiner Script Class.
/// </summary>
internal class Script
{
	/// <summary>
	/// The script entry point.
	/// </summary>
	/// <param name="engine">Provides access to the Automation engine.</param>
	/// <param name="context">Provides access to the installation context.</param>
	[AutomationEntryPoint(AutomationEntryPointType.Types.InstallAppPackage)]
	public void Install(IEngine engine, AppInstallContext context)
	{
		try
		{
			engine.Timeout = new TimeSpan(0, 10, 0);
			engine.GenerateInformation("Starting installation");
			var installer = new AppInstaller(Engine.SLNetRaw, context);
			installer.InstallDefaultContent();
			SnmpWalkConfigurationSchema.EnsureProvisioned(engine);
			installer.InstallUserDefinedApiDefinitions(engine);
			SecurePath setupContentPath = SecurePath.CreateSecurePath(installer.GetSetupContentDirectory());
			SecurePath sourceFrontendPath = SecurePath.ConstructSecurePath(setupContentPath, "SLC-S-SNMPWalkBrowser");
			SecurePath webpagesPublicPath = SecurePath.CreateSecurePath("C:\\Skyline DataMiner\\Webpages\\Public");
			SecurePath destinationFrontendPath = SecurePath.ConstructSecurePath(webpagesPublicPath, "SLC-S-SNMPWalkBrowser");

			if (!Directory.Exists(sourceFrontendPath))
			{
				engine.GenerateInformation($"Frontend package content not found at '{sourceFrontendPath}'.");
				return;
			}

			CopyDirectory(sourceFrontendPath, destinationFrontendPath);
			engine.GenerateInformation($"Frontend files deployed to '{destinationFrontendPath}'.");

			// Custom installation logic can be added here for each individual install package.
		}
		catch (Exception e)
		{
			engine.ExitFail($"Exception encountered during installation: {e}");
		}
	}

	private static void CopyDirectory(SecurePath sourceDirectory, SecurePath destinationDirectory)
	{
		Directory.CreateDirectory(destinationDirectory);

		foreach (string file in Directory.GetFiles(sourceDirectory))
		{
			SecurePath sourceFile = SecurePath.CreateSecurePath(file);
			SecurePath destinationFile = SecurePath.ConstructSecurePath(destinationDirectory, Path.GetFileName(file));
			File.Copy(sourceFile, destinationFile, overwrite: true);
		}

		foreach (string directory in Directory.GetDirectories(sourceDirectory))
		{
			SecurePath sourceSubdirectory = SecurePath.CreateSecurePath(directory);
			SecurePath destinationSubdirectory = SecurePath.ConstructSecurePath(destinationDirectory, Path.GetFileName(directory));
			CopyDirectory(sourceSubdirectory, destinationSubdirectory);
		}
	}
}