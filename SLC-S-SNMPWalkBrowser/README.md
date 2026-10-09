# SNMP Walk Browser

Read-only React application for browsing completed `SLC-S-SNMPWalkCollector` evidence bundles. It displays collection metadata, root health, a bounded OID tree, and binding search results. It does not send SNMP requests or expose the collector's output directory to a browser.

## Evidence contract

The browser consumes only completed schema-v2 bundles:

- `.walk.metadata.json` is the completion marker and names the paired raw file.
- The raw `.walk` file is UTF-8 JSON Lines with `oid` and `value` properties.
- Partial collection outcomes remain visible; they are not treated as end-of-MIB.

## API integration

The browser calls the packaged `SLC-S-SNMPWalkExplorer.Bridge` Automation script through DataMiner's same-origin `/API/v1/Json.asmx/ExecuteAutomationScriptWithOutput` endpoint. It uses the session established by the DataMiner authentication entry point; no API token, DataMiner host, or credential value is embedded in the build.

The bridge exposes artifact listing, raw download, artifact deletion, binding search, OID-tree navigation, configuration record CRUD operations, and asynchronous walk execution. Every evidence operation resolves an `artifactId` through a metadata-complete bundle first. The tree operation streams at most 1,000 valid bindings from the committed raw artifact and retains the raw-file size restriction, so the browser never loads an arbitrary evidence file or accesses the DataMiner Agent file share directly.

- **Saved configurations**: Contain non-secret settings only. Triggering **Execute walk** from the browser prompts for the SNMP community string just-in-time and executes `SLC-S-SNMPWalkCollector` as an asynchronous background subscript on the DMA.
- **Evidence deletion**: The **Delete walk** action removes both the raw `.walk` data and its `.walk.metadata.json` marker through the authenticated bridge.

## Local development

```powershell
npm install
npm run dev
npm run test:e2e
```

Build the static deployment output with:

```powershell
npm run build
```

The Vite base is relative, so `dist/` can be hosted beneath the eventual DataMiner application path. The package build stages this output in `SetupContent/SLC-S-SNMPWalkBrowser`; package installation copies it to DataMiner's public web folder. Open the deployed application through DataMiner authentication at:

```text
{PROTOCOL}://{DOMAIN}/auth/?url=%2Fpublic%2FSLC-S-SNMPWalkBrowser%2Findex.html
```
