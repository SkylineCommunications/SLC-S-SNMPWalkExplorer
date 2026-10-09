# SNMP Walk Rules

## Objective

A walk must retain every accessible binding beneath every configured discovery root without treating an adapter failure as a valid end of the subtree.

## Runtime Prerequisite

The collector loads `Skyline.DataMiner.QA.SNMP.dll` from `C:\Skyline DataMiner\Tools\QADeviceSimulator` on the executing DMA. This library is installed with DataMiner; verify that the file exists before running the collector. No separate QA Device Simulator installer is required.

## Current collector contract

- Probe SNMPv1 and SNMPv2c, then use the highest working version.
- Independently walk configured, distinct, non-overlapping numeric prefixes; a failed prefix cannot prevent the other prefixes from running.
- SNMPv1 issues one-OID GETNEXT requests and retries each request up to the configured retry count.
- GETNEXT is the default collection mechanism. SNMPv2c GETBULK is opt-in and requires live-target validation before production use.
- The implementation records `end-of-mib` only when the QA adapter returns `SNMP End-of-MIB-View`; an empty or failed adapter response is not treated as completion.
- Enforce a single shared binding cap across all workers. The default is effectively unlimited; when a configured cap is reached, the active prefix reports `global-safety-cap` and remaining work retains its own visible outcome.
- Worker concurrency is bounded. Prefixes must be configured as non-overlapping numeric OIDs, so each published binding has exactly one owner.
- SNMP cannot generically discover deep sibling subtrees without traversing them. Large completed prefixes are marked as partition recommendations for a subsequent run; this is evidence, not a vendor-specific assumption.

## Root terminal states

| State | Meaning |
| --- | --- |
| `outside-subtree` | A valid returned OID was outside the configured root. |
| `end-of-mib` | A GETBULK response exposed the QA library `SNMP End-of-MIB-View` value. |
| `global-safety-cap` | The shared binding budget was exhausted while this prefix was active. |
| `request-failed` | GETNEXT did not succeed after its configured retries. |
| `unexpected-binding-count` | GETNEXT returned a response other than one binding. |
| `non-increasing-oid` | The returned OID did not advance numerically. |
| `repeated-oid` | A previously returned OID reappeared in the root sequence. |
| `invalid-oid` | The response OID could not be parsed as numeric arcs. |
| `skipped-global-safety-cap` | A prior root exhausted the global cap before this root began. |

Each root outcome retains its collected bindings, root OID, binding count, last successful OID, terminal state, optional error, and retry count.

## Publication contract

Each execution emits paired evidence in the configured output directory:

- An immutable `.walk` file in UTF-8 JSON Lines format: one `{"oid":"...","value":"..."}` object per binding. Values are JSON-escaped, so embedded newlines cannot corrupt record boundaries.
- An adjacent `.walk.metadata.json` sidecar containing schema version `2`, start/completion timestamps, target address and port, selected SNMP version, total bindings, raw format, overall completeness, and all per-prefix outcomes. It must never contain the community value.

Both files are first written to same-directory `.partial` paths. The raw `.walk` file is published first; the metadata sidecar is published last and is the commit marker. Even when zero bindings are collected, publish both evidence files before failing the Automation script.

## Protocol Boundaries

- GETBULK is SNMPv2c-only. Set `GetBulkDiagnosticOid` to a numeric OID to run one isolated, one-repetition GETBULK diagnostic before a normal walk. It does not enable GETBULK for the full walk.
- Gaps between OIDs and sparse table indexes are legal; only root boundaries and the terminal states above end a root.