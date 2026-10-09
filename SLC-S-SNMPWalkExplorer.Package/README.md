# SNMP Walk Explorer Package

Building this project runs the browser production build and stages its `dist` output in `SetupContent/SLC-S-SNMPWalkBrowser`. Installation deploys the browser to `C:\Skyline DataMiner\Webpages\Public\SLC-S-SNMPWalkBrowser` and installs the User-Defined API, collector, and DOM configuration schema.

Install Node dependencies first:

```powershell
Set-Location ..\SLC-S-SNMPWalkBrowser
npm ci
```

To build package code without rebuilding the frontend, set `SkipFrontendBuild=true` only when current browser output has already been staged.