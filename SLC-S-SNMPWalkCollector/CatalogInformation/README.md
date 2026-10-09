# SNMP Walk Collector

## About

The **SNMP Walk Collector** is an automated DataMiner solution component designed to gather comprehensive SNMP evidence from network devices. It probes SNMPv1 and SNMPv2c availability, walks configured non-overlapping numeric OID roots independently with worker concurrency, and writes committed, immutable JSON Lines evidence bundles directly on the executing DataMiner Agent.

The collector produces paired artifact files (`.walk` data and `.walk.metadata.json` verification marker) without storing or leaking credential secrets, establishing an auditable baseline for network discovery and device simulation.

## Key Features

- **Protocol Auto-Negotiation**: Probes both SNMPv1 and SNMPv2c, automatically preferring SNMPv2c when both succeed.
- **Concurrent Root Walks**: Partitions walk tasks across independent worker limits to optimize collection speed.
- **Atomic Evidence Commitment**: Publishes raw UTF-8 JSON Lines evidence first and a metadata completion marker last.
- **Diagnostic Safety Gates**: Limits total collected bindings, detects lexicographical loops, and issues partition recommendations when root sizes exceed bounds.
- **Zero Credential Retention**: Sanitizes output so that SNMP community strings and sensitive parameters are never recorded in evidence files.

## Use Cases

- **Baseline Device Capture**: Capture complete MIB walks for new vendor hardware during onboarding or firmware upgrades.
- **Diagnostic Troubleshooting**: Isolate device-side SNMP agent errors, timeouts, or counter roll-overs under controlled execution.
- **Connector Validation**: Supply clean, verifiable walk dumps for connector unit testing and simulation.

## Prerequisites

- **DataMiner Version**: DataMiner `10.5.0.0-15822` or later.
- **QA SNMP Library**: `Skyline.DataMiner.QA.SNMP.dll` located at `C:\Skyline DataMiner\Tools\QADeviceSimulator\Skyline.DataMiner.QA.SNMP.dll` on executing DMAs. This library is installed with DataMiner; no separate QA Device Simulator installation is required.

## Technical Reference

Configure the Automation script parameters for target address, port, community, timeouts, retries, worker limits, discovery roots, and collection limits.

Output evidence is published to:
`C:\Skyline DataMiner\Documents\SLC-S-SNMPWalkCollector`

For detailed technical rules and specifications:
- [SNMP Walk Rules & Terminal States](https://github.com/SkylineCommunications/SLC-S-SNMPWalkExplorer/blob/main/docs/SNMP-Walk-Rules.md)
- [Operational Validation](https://github.com/SkylineCommunications/SLC-S-SNMPWalkExplorer/blob/main/docs/Operational-Validation.md)
- [Contact DataMiner Technical Support](https://aka.dataminer.services/contacting-tech-support)








