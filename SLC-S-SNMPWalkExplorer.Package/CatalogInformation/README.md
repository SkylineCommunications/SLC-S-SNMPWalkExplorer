# SNMP Walk Explorer

SNMP Walk Explorer is a DataMiner solution for reviewing evidence published by the SNMP Walk Collector.

It installs:

- a User-Defined API for completed evidence bundles and non-secret DOM configuration records;
- a browser application for metadata, root outcomes, bounded OID-tree navigation, binding search, and raw-evidence download; and
- the collector Automation script.

The minimum supported DataMiner version is `10.6.0.0-17344`. The collector uses the QA Device Simulator SNMP library installed with DataMiner on every executing DMA; no separate QA Simulator installer is required.

After installation, open the browser through DataMiner authentication:

```text
{PROTOCOL}://{DOMAIN}/auth/?url=%2Fpublic%2FSLC-S-SNMPWalkBrowser%2Findex.html
```

Configuration records retain settings only; they do not start an SNMP walk.