# SNMP Walk Collector

The collector is a DataMiner Automation script that probes SNMPv1 and SNMPv2c, then independently walks configured, non-overlapping numeric OID roots. It publishes immutable UTF-8 JSON Lines evidence to `C:\Skyline DataMiner\Documents\SLC-S-SNMPWalkCollector` on the executing DMA.

## Prerequisite

The collector uses `Skyline.DataMiner.QA.SNMP.dll` at `C:\Skyline DataMiner\Tools\QADeviceSimulator\Skyline.DataMiner.QA.SNMP.dll`. This library is installed with DataMiner; verify that the file exists on each DMA that executes the script. No separate QA Device Simulator installer is required.

## Configuration and Output

Configure the Automation script parameters for the target address, port, community, timeouts, retries, worker limit, discovery roots, collection cap, and optional GETBULK diagnostic. The collector prefers SNMPv2c when both protocol probes succeed.

Each execution creates a paired evidence bundle:

- `.walk`: one `{"oid":"...","value":"..."}` object per line.
- `.walk.metadata.json`: collection metadata and one outcome per configured root.

The raw file is published first and metadata is published last as the completion marker. Neither file contains the SNMP community value.

See the repository [collection rules](../../docs/SNMP-Walk-Rules.md) for terminal states and safety behavior.








