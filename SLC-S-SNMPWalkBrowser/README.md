# SNMP Walk Browser

Read-only React application for browsing completed `SLC-S-SNMPWalkCollector` evidence bundles. It displays collection metadata, root health, a bounded OID tree, and binding search results. It does not send SNMP requests or expose the collector's output directory to a browser.

## Evidence contract

The browser consumes only completed schema-v2 bundles:

- `.walk.metadata.json` is the completion marker and names the paired raw file.
- The raw `.walk` file is UTF-8 JSON Lines with `oid` and `value` properties.
- Partial collection outcomes remain visible; they are not treated as end-of-MIB.

## API integration

The browser uses the deployed DataMiner User-Defined API at the same-origin default `/api/v1/custom/snmp-walk-explorer`. No DataMiner host or domain is embedded in the build. Set `VITE_WALK_API_BASE_URL` only when a different same-origin API route is required:

```powershell
$env:VITE_WALK_API_BASE_URL = '/api/v1/custom/snmp-walk-explorer'
npm run build
```

The API exposes artifact listing, raw download, binding search, OID-tree navigation, and configuration record list/create operations. Every evidence endpoint resolves `id` through a metadata-complete bundle first. The tree endpoint streams at most 1,000 valid bindings from the committed raw artifact and retains the raw-file size restriction, so the browser never loads an arbitrary evidence file or accesses the DataMiner Agent file share directly.

Saved configurations contain non-secret settings only. They do not start collector executions.

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
