import { test } from '@playwright/test'
import * as path from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const sampleArtifacts = {
  artifacts: [
    {
      id: 'core-switch-01',
      rawFileName: 'core-switch-01_20261008_143000.walk',
      startedAtUtc: '2026-10-08T14:30:00.0000000Z',
      completedAtUtc: '2026-10-08T14:34:22.0000000Z',
      targetAddress: '10.200.1.1',
      targetPort: 161,
      snmpVersion: 'SNMPv2c',
      concurrentWalkWorkers: 4,
      useGetBulk: true,
      totalBindings: 4825,
      isComplete: true,
      rootOutcomes: [
        {
          rootOid: '1.3.6.1.2.1.1',
          terminalState: 'Complete',
          isComplete: true,
          bindingCount: 18,
          retryCount: 0,
          lastOid: '1.3.6.1.2.1.1.9.1.4.8',
          partitionRecommended: false,
        },
        {
          rootOid: '1.3.6.1.2.1.2',
          terminalState: 'Complete',
          isComplete: true,
          bindingCount: 1248,
          retryCount: 0,
          lastOid: '1.3.6.1.2.1.2.2.1.22.48',
          partitionRecommended: false,
        },
        {
          rootOid: '1.3.6.1.2.1.4',
          terminalState: 'Complete',
          isComplete: true,
          bindingCount: 642,
          retryCount: 0,
          lastOid: '1.3.6.1.2.1.4.35.1.7.1.4.10.200.1.1',
          partitionRecommended: false,
        },
        {
          rootOid: '1.3.6.1.4.1.9',
          terminalState: 'Complete',
          isComplete: true,
          bindingCount: 2917,
          retryCount: 1,
          lastOid: '1.3.6.1.4.1.9.9.109.1.1.1.1.8.1',
          partitionRecommended: false,
        },
      ],
    },
    {
      id: 'edge-router-02',
      rawFileName: 'edge-router-02_20261008_121500.walk',
      startedAtUtc: '2026-10-08T12:15:00.0000000Z',
      completedAtUtc: '2026-10-08T12:18:45.0000000Z',
      targetAddress: '10.200.2.254',
      targetPort: 161,
      snmpVersion: 'SNMPv2c',
      concurrentWalkWorkers: 2,
      useGetBulk: false,
      totalBindings: 1842,
      isComplete: true,
      rootOutcomes: [
        {
          rootOid: '1.3.6.1.2.1.1',
          terminalState: 'Complete',
          isComplete: true,
          bindingCount: 16,
          retryCount: 0,
          lastOid: '1.3.6.1.2.1.1.8.0',
          partitionRecommended: false,
        },
        {
          rootOid: '1.3.6.1.2.1.2',
          terminalState: 'Complete',
          isComplete: true,
          bindingCount: 850,
          retryCount: 0,
          lastOid: '1.3.6.1.2.1.2.2.1.22.12',
          partitionRecommended: false,
        },
        {
          rootOid: '1.3.6.1.2.1.31',
          terminalState: 'Complete',
          isComplete: true,
          bindingCount: 976,
          retryCount: 0,
          lastOid: '1.3.6.1.2.1.31.1.1.1.18.12',
          partitionRecommended: false,
        },
      ],
    },
    {
      id: 'dist-firewall-01',
      rawFileName: 'dist-firewall-01_20261007_090000.walk',
      startedAtUtc: '2026-10-07T09:00:00.0000000Z',
      completedAtUtc: '2026-10-07T09:05:10.0000000Z',
      targetAddress: '192.168.100.1',
      targetPort: 161,
      snmpVersion: 'SNMPv1',
      concurrentWalkWorkers: 1,
      useGetBulk: false,
      totalBindings: 980,
      isComplete: false,
      rootOutcomes: [
        {
          rootOid: '1.3.6.1.2.1.1',
          terminalState: 'Complete',
          isComplete: true,
          bindingCount: 14,
          retryCount: 0,
          lastOid: '1.3.6.1.2.1.1.7.0',
          partitionRecommended: false,
        },
        {
          rootOid: '1.3.6.1.4.1.2620',
          terminalState: 'RequestTimeout',
          isComplete: false,
          bindingCount: 966,
          retryCount: 3,
          lastOid: '1.3.6.1.4.1.2620.1.1.25.1.0',
          error: 'Agent timed out after 3 retries at OID 1.3.6.1.4.1.2620.1.1.25.1.0',
          partitionRecommended: true,
        },
      ],
    },
  ],
}

const sampleTree = {
  oid: 'root',
  label: 'Observed OIDs',
  bindings: 4825,
  children: [
    {
      oid: '1.3.6.1',
      label: 'internet',
      bindings: 4825,
      children: [
        {
          oid: '1.3.6.1.2',
          label: 'mgmt',
          bindings: 1908,
          children: [
            {
              oid: '1.3.6.1.2.1',
              label: 'mib-2',
              bindings: 1908,
              children: [
                {
                  oid: '1.3.6.1.2.1.1',
                  label: 'system',
                  bindings: 18,
                  children: [
                    { oid: '1.3.6.1.2.1.1.1.0', label: 'sysDescr.0', bindings: 1, value: 'Cisco IOS Software, C3750E Software (C3750E-UNIVERSALK9-M), Version 15.2(4)E10' },
                    { oid: '1.3.6.1.2.1.1.2.0', label: 'sysObjectID.0', bindings: 1, value: '1.3.6.1.4.1.9.1.516' },
                    { oid: '1.3.6.1.2.1.1.3.0', label: 'sysUpTime.0', bindings: 1, value: '234892182 (27 days, 04:28:41.82)' },
                    { oid: '1.3.6.1.2.1.1.4.0', label: 'sysContact.0', bindings: 1, value: 'Network Operations <noc@skyline.be>' },
                    { oid: '1.3.6.1.2.1.1.5.0', label: 'sysName.0', bindings: 1, value: 'core-sw01.brussels.skyline.be' },
                    { oid: '1.3.6.1.2.1.1.6.0', label: 'sysLocation.0', bindings: 1, value: 'DC-1 Rack B04' },
                  ],
                },
                {
                  oid: '1.3.6.1.2.1.2',
                  label: 'interfaces',
                  bindings: 1248,
                  children: [
                    { oid: '1.3.6.1.2.1.2.1.0', label: 'ifNumber.0', bindings: 1, value: '54' },
                    {
                      oid: '1.3.6.1.2.1.2.2',
                      label: 'ifTable',
                      bindings: 1247,
                      children: [
                        {
                          oid: '1.3.6.1.2.1.2.2.1',
                          label: 'ifEntry',
                          bindings: 1247,
                          children: [
                            { oid: '1.3.6.1.2.1.2.2.1.2.1', label: 'ifDescr.1', bindings: 1, value: 'GigabitEthernet1/0/1' },
                            { oid: '1.3.6.1.2.1.2.2.1.2.2', label: 'ifDescr.2', bindings: 1, value: 'GigabitEthernet1/0/2' },
                            { oid: '1.3.6.1.2.1.2.2.1.8.1', label: 'ifOperStatus.1', bindings: 1, value: 'up(1)' },
                            { oid: '1.3.6.1.2.1.2.2.1.8.2', label: 'ifOperStatus.2', bindings: 1, value: 'up(1)' },
                          ],
                        },
                      ],
                    },
                  ],
                },
              ],
            },
          ],
        },
        {
          oid: '1.3.6.1.4',
          label: 'private',
          bindings: 2917,
          children: [
            {
              oid: '1.3.6.1.4.1',
              label: 'enterprises',
              bindings: 2917,
              children: [
                {
                  oid: '1.3.6.1.4.1.9',
                  label: 'cisco',
                  bindings: 2917,
                  children: [],
                },
              ],
            },
          ],
        },
      ],
    },
  ],
}

const sampleConfigurations = [
  {
    id: 'cfg-core-switches',
    name: 'Core Switches Brussels DC',
    targetAddress: '10.200.1.1',
    targetPort: 161,
    credentialReference: 'sec-snmp-dc1',
    timeoutMilliseconds: 3000,
    retries: 2,
    logLevel: 1,
    maximumWalkVariables: 200000,
    concurrentWalkWorkers: 4,
    useGetBulk: true,
    bulkMaxRepetitions: 25,
    partitionRecommendationBindings: 10000,
    getBulkDiagnosticOid: '1.3.6.1.2.1.1.1.0',
    discoveryRoots: '1.3.6.1.2.1.1; 1.3.6.1.2.1.2; 1.3.6.1.4.1.9',
  },
  {
    id: 'cfg-edge-routers',
    name: 'WAN Edge Routers',
    targetAddress: '10.200.2.254',
    targetPort: 161,
    credentialReference: 'sec-snmp-wan',
    timeoutMilliseconds: 5000,
    retries: 3,
    logLevel: 1,
    maximumWalkVariables: 100000,
    concurrentWalkWorkers: 2,
    useGetBulk: false,
    bulkMaxRepetitions: 10,
    partitionRecommendationBindings: 5000,
    getBulkDiagnosticOid: '',
    discoveryRoots: '1.3.6.1.2.1.1; 1.3.6.1.2.1.2; 1.3.6.1.2.1.31',
  },
]

function bridgeResult(result: unknown) {
  return { d: { ScriptOutput: [{ Name: 'Result', Value: JSON.stringify(result) }] } }
}

async function mockBridge(page: import('@playwright/test').Page) {
  await page.context().addCookies([
    { name: 'DMAConnection', value: 'production-dma-01', domain: '127.0.0.1', path: '/' },
    { name: 'DMAUser', value: JSON.stringify({ Name: 'Operator Shawn' }), domain: '127.0.0.1', path: '/' },
  ])
  await page.route('**/API/v1/Json.asmx/ExecuteAutomationScriptWithOutput', (route) => {
    const body = route.request().postDataJSON() as {
      connection: string
      script: {
        Parameters: Array<{ Description?: string; Name?: string; Value: string }>
      }
    }
    const parameters = Object.fromEntries(body.script.Parameters.map((parameter) => [parameter.Name ?? parameter.Description, parameter.Value]))
    const action = parameters.Action
    if (action === 'ListArtifacts') {
      return route.fulfill({ json: bridgeResult(sampleArtifacts) })
    }
    if (action === 'ListConfigurations') {
      return route.fulfill({ json: bridgeResult(sampleConfigurations) })
    }
    if (action === 'GetArtifactTree') {
      return route.fulfill({ json: bridgeResult(sampleTree) })
    }
    if (action === 'SearchBindings') {
      return route.fulfill({
        json: bridgeResult([
          { oid: '1.3.6.1.2.1.2.2.1.2.1', value: 'GigabitEthernet1/0/1' },
          { oid: '1.3.6.1.2.1.2.2.1.2.2', value: 'GigabitEthernet1/0/2' },
        ]),
      })
    }
    return route.fulfill({ json: bridgeResult({}) })
  })
}

test('capture real screenshots of SNMP Walk Explorer UI', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 })
  await mockBridge(page)

  // 1. Overview tab screenshot
  await page.goto('/')
  await page.waitForSelector('.run-item')
  await page.waitForTimeout(500)

  const overviewPath = path.resolve(__dirname, '../../docs/images/snmp-walk-explorer-overview.png')
  await page.screenshot({ path: overviewPath, fullPage: false })

  // Also save the hero image as the overview
  const heroPath = path.resolve(__dirname, '../src/assets/hero.png')
  await page.screenshot({ path: heroPath, fullPage: false })

  const packageHeroPath = path.resolve(__dirname, '../../SLC-S-SNMPWalkExplorer.Package/CatalogInformation/Images/hero.png')
  await page.screenshot({ path: packageHeroPath, fullPage: false })

  const docsHeroPath = path.resolve(__dirname, '../../docs/images/snmp-walk-explorer.png')
  await page.screenshot({ path: docsHeroPath, fullPage: false })

  // 2. Explorer / OID Tree tab screenshot
  await page.getByRole('button', { name: 'Explorer' }).click()
  await page.waitForSelector('.tree-panel')
  // Expand tree nodes
  await page.getByRole('button', { name: 'Expand all' }).click()
  await page.waitForTimeout(500)

  const explorerPath = path.resolve(__dirname, '../../docs/images/snmp-walk-explorer-tree.png')
  await page.screenshot({ path: explorerPath, fullPage: false })

  // 3. Configurations tab screenshot
  await page.getByRole('button', { name: 'Configurations' }).click()
  await page.waitForSelector('.configuration-form')
  await page.getByRole('button', { name: /Core Switches Brussels DC/ }).click()
  await page.waitForTimeout(500)

  const configsPath = path.resolve(__dirname, '../../docs/images/snmp-walk-explorer-configurations.png')
  await page.screenshot({ path: configsPath, fullPage: false })
})