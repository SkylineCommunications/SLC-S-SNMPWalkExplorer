import { expect, test } from '@playwright/test'

const artifacts = {
  artifacts: [
    {
      id: 'first-run',
      rawFileName: 'first-run.walk',
      startedAtUtc: '2026-01-01T00:00:00.0000000Z',
      completedAtUtc: '2026-01-01T00:01:00.0000000Z',
      targetAddress: '192.0.2.10',
      targetPort: 161,
      snmpVersion: 'SNMPv2c',
      concurrentWalkWorkers: 2,
      useGetBulk: false,
      totalBindings: 1,
      isComplete: true,
      rootOutcomes: [],
    },
    {
      id: 'second-run',
      rawFileName: 'second-run.walk',
      startedAtUtc: '2026-01-02T00:00:00.0000000Z',
      completedAtUtc: '2026-01-02T00:01:00.0000000Z',
      targetAddress: '192.0.2.11',
      targetPort: 161,
      snmpVersion: 'SNMPv2c',
      concurrentWalkWorkers: 2,
      useGetBulk: false,
      totalBindings: 2,
      isComplete: true,
      rootOutcomes: [],
    },
  ],
}

test('loads and refreshes the observed OID hierarchy for the selected artifact', async ({ page }) => {
  const requestedTreeIds: string[] = []
  await page.route('**/api/v1/custom/snmp-walk-explorer/artifacts', (route) => route.fulfill({ json: artifacts }))
  await page.route('**/api/v1/custom/snmp-walk-explorer/artifacts/*/tree', (route) => {
    const artifactId = route.request().url().split('/').slice(-2)[0]
    requestedTreeIds.push(artifactId)
    const bindings = artifactId === 'first-run' ? 1 : 2
    return route.fulfill({
      json: {
        oid: 'root',
        label: 'Observed OIDs',
        bindings,
        children: [],
      },
    })
  })

  await page.goto('/')
  await page.getByRole('button', { name: 'Explorer' }).click()
  const root = page.getByRole('button', { name: /Observed OIDs/ })
  await expect(root).toContainText('1 bindings')

  await page.locator('.run-item').filter({ hasText: '192.0.2.11' }).click()
  await expect(root).toContainText('2 bindings')
  await expect.poll(() => requestedTreeIds).toEqual(['first-run', 'second-run'])
})

test('saves a configuration through the dedicated create route', async ({ page }) => {
  let requestMethod = ''
  let requestBody: { name: string } | undefined

  await page.route('**/api/v1/custom/snmp-walk-explorer/artifacts', (route) => route.fulfill({ json: { artifacts: [] } }))
  await page.route('**/api/v1/custom/snmp-walk-explorer/configs', (route) => route.fulfill({ json: [] }))
  await page.route('**/api/v1/custom/snmp-walk-explorer/configs/create', (route) => {
    requestMethod = route.request().method()
    requestBody = route.request().postDataJSON() as { name: string }
    return route.fulfill({ json: { ...requestBody, id: 'saved-configuration' } })
  })

  await page.goto('/')
  await page.getByRole('button', { name: 'Configurations' }).click()
  await page.getByLabel('Name').fill('Edge router')
  await page.getByLabel('Target address').fill('192.0.2.10')
  await page.getByLabel('Credential reference').fill('snmp-readonly')
  await page.getByRole('button', { name: 'Save configuration' }).click()

  await expect.poll(() => requestMethod).toBe('POST')
  expect(requestBody?.name).toBe('Edge router')
  await expect(page.getByText('Edge router', { exact: true })).toBeVisible()
})