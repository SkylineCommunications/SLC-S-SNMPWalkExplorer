# Operational Validation

## Automated Checks

Run these checks before publishing a package:

```powershell
Set-Location SLC-S-SNMPWalkBrowser
npm ci
npm run lint
npm run build
npm run test:e2e

Set-Location ..
dotnet build .\SLC-S-SNMPWalkExplorer.slnx -c Release --warnaserror
dotnet test .\SLC-S-SNMPWalkCollector.Tests\SLC-S-SNMPWalkCollector.Tests.csproj -c Release --no-build
```

The .NET tests cover OID validation and ordering, settings validation, retry behavior, terminal states, collection-budget behavior, metadata-pair visibility, raw-artifact access, bounded binding search, and bounded tree generation. Browser E2E tests use mocked authenticated Automation bridge responses.

## DMA Validation

Validate a release on a controlled SNMP target:

1. Run a successful walk and compare the binding count and final OID with an independent SNMP tool or a known-good DataMiner element.
2. Run a partial-root failure and verify that completed roots and their metadata outcomes remain available in Explorer.
3. Run a zero-binding failure and verify that both the raw evidence and metadata sidecar are published before Automation reports failure.
4. Run the isolated GETBULK diagnostic before enabling GETBULK for a production collection.
5. Verify Explorer artifact listing, search, tree, raw download, and configuration save/load through the deployed `SLC-S-SNMPWalkExplorer.Bridge` Automation script while signed in through DataMiner authentication.
6. Verify walk execution trigger from the **Configurations** tab: select a target, click **Execute walk**, provide the community string, and verify that the collector subscript starts asynchronously in background without web UI timeouts.
7. Verify evidence cleanup from the **Overview** tab: click **Delete walk** on a selected artifact, accept the confirmation, and verify that both `<name>.walk` and `<name>.walk.metadata.json` are removed from `C:\Skyline DataMiner\Documents\SLC-S-SNMPWalkCollector`.

Record the target DMA version and validation outcome with the release. Do not raise `MinimumRequiredDmVersion` based only on referenced package versions.