namespace SLCSSNMPWalkExplorerApi
{
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis.Actions;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;

	/// <summary>
	/// Represents a DataMiner user-defined API.
	/// </summary>
	public static class Script
	{
		private static IUserDefinedApi api;

		/// <summary>
		/// The API trigger.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		/// <param name="requestData">Holds the API request data.</param>
		/// <returns>An object with the script API output data.</returns>
		[AutomationEntryPoint(AutomationEntryPointType.Types.OnApiTrigger)]
		public static ApiTriggerOutput OnApiTrigger(IEngine engine, ApiTriggerInput requestData)
		{
			if (api == null)
			{
				api = UserDefinedApi.CreateBuilder().AddControllers().Build();
			}

			return api.Run(engine, requestData);
		}
	}

	/// <summary>
	/// Exposes read-only SNMP walk artifact endpoints.
	/// </summary>
	[ApiController]
	[Route("snmp-walk-explorer")]
	public sealed class WalkArtifactsController : ControllerBase
	{
		/// <summary>
		/// Returns a health response for the explorer API.
		/// </summary>
		[HttpGet("health")]
		[Produces("application/json")]
		public IApiResult GetHealth()
		{
			return Ok("{\"status\":\"ok\"}");
		}

		/// <summary>
		/// Returns metadata-backed, committed SNMP walk artifacts.
		/// </summary>
		[HttpGet("artifacts")]
		[Produces("application/json")]
		public IApiResult GetArtifacts()
		{
			WalkArtifactCatalog catalog = new WalkArtifactCatalog(WalkArtifactCatalog.DefaultArtifactDirectory);
			return Ok(catalog.SerializeArtifacts());
		}

		/// <summary>
		/// Returns the raw JSON Lines evidence file for one committed artifact.
		/// </summary>
		/// <param name="id">The artifact identifier.</param>
		/// <returns>The raw walk evidence or a not-found response.</returns>
		[HttpGet("artifacts/{id}/raw")]
		[Produces("application/x-ndjson")]
		public IApiResult GetRawArtifact(string id)
		{
			WalkArtifactCatalog catalog = new WalkArtifactCatalog(WalkArtifactCatalog.DefaultArtifactDirectory);
			string rawArtifact;
			if (!catalog.TryReadRawArtifact(id, out rawArtifact))
			{
				return NotFound("The requested committed walk artifact was not found.");
			}

			return Ok(rawArtifact);
		}

		/// <summary>
		/// Searches bounded binding evidence for one committed artifact.
		/// </summary>
		/// <param name="id">The artifact identifier.</param>
		/// <param name="prefix">An optional numeric OID prefix.</param>
		/// <param name="query">An optional value substring.</param>
		/// <returns>Matching bindings or a validation/not-found response.</returns>
		[HttpGet("artifacts/{id}/bindings")]
		[Produces("application/json")]
		public IApiResult SearchBindings(string id, [FromQuery] string prefix = null, [FromQuery] string query = null)
		{
			try
			{
				WalkArtifactCatalog catalog = new WalkArtifactCatalog(WalkArtifactCatalog.DefaultArtifactDirectory);
				string bindings;
				if (!catalog.TrySearchBindings(id, prefix, query, out bindings))
				{
					return NotFound("The requested committed walk artifact was not found.");
				}

				return Ok(bindings);
			}
			catch (System.ArgumentException exception)
			{
				return BadRequest(exception.Message);
			}
		}

		/// <summary>
		/// Returns a bounded OID hierarchy for one committed artifact.
		/// </summary>
		/// <param name="id">The artifact identifier.</param>
		/// <returns>The observed hierarchy or a not-found response.</returns>
		[HttpGet("artifacts/{id}/tree")]
		[Produces("application/json")]
		public IApiResult GetTree(string id)
		{
			WalkArtifactCatalog catalog = new WalkArtifactCatalog(WalkArtifactCatalog.DefaultArtifactDirectory);
			string tree;
			if (!catalog.TryBuildTree(id, out tree))
			{
				return NotFound("The requested committed walk artifact was not found.");
			}

			return Ok(tree);
		}
	}

	/// <summary>
	/// Exposes persisted, non-secret SNMP walk configurations.
	/// </summary>
	[ApiController]
	[Route("snmp-walk-explorer")]
	public sealed class WalkConfigurationsController : ControllerBase
	{
		/// <summary>
		/// Lists saved configurations.
		/// </summary>
		[HttpGet("configs")]
		[Produces("application/json")]
		public IApiResult GetConfigurations(IEngine engine)
		{
			return Ok(new WalkConfigurationRepository(engine).List());
		}

		/// <summary>
		/// Saves a non-secret configuration.
		/// </summary>
		[HttpPost("configs")]
		[Produces("application/json")]
		public IApiResult CreateConfiguration(IEngine engine, [FromBody] WalkConfiguration configuration)
		{
			try
			{
				return Ok(new WalkConfigurationRepository(engine).Create(configuration));
			}
			catch (System.ArgumentException exception)
			{
				return BadRequest(exception.Message);
			}
		}
	}
}