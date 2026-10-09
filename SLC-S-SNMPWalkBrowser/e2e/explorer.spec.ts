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

function bridgeResult(result: unknown) {
  return { d: { ScriptOutput: [{ Name: 'Result', Value: JSON.stringify(result) }] } }
}

async function mockBridge(page: import('@playwright/test').Page, handler: (action: string, request: unknown) => unknown) {
  await page.context().addCookies([
    { name: 'DMAConnection', value: 'test-connection', domain: '127.0.0.1', path: '/' },
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
    return route.fulfill({ json: bridgeResult(handler(parameters.Action, JSON.parse(parameters.RequestJson))) })
  })
}

test('loads and refreshes the observed OID hierarchy for the selected artifact', async ({ page }) => {
  const requestedTreeIds: string[] = []
  await mockBridge(page, (action, request) => {
    if (action === 'ListArtifacts') return artifacts
    if (action === 'ListConfigurations') return []
    if (action === 'GetArtifactTree') {
      const artifactId = (request as { artifactId: string }).artifactId
    requestedTreeIds.push(artifactId)
    const bindings = artifactId === 'first-run' ? 1 : 2
      return {
        oid: 'root',
        label: 'Observed OIDs',
        bindings,
        children: [],
      }
    }
    throw new Error(`Unexpected bridge action: ${action}`)
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
  let requestBody: { name: string } | undefined

  await mockBridge(page, (action, request) => {
    if (action === 'ListArtifacts') return { artifacts: [] }
    if (action === 'ListConfigurations') return []
    if (action === 'CreateConfiguration') {
      requestBody = request as { name: string }
      return { ...requestBody, id: 'saved-configuration' }
    }
    throw new Error(`Unexpected bridge action: ${action}`)
  })

  await page.goto('/')
  await page.getByRole('button', { name: 'Configurations' }).click()
  await page.getByLabel('Name').fill('Edge router')
  await page.getByLabel('Target address').fill('192.0.2.10')
  await page.getByLabel('Credential reference').fill('snmp-readonly')
  await page.getByRole('button', { name: 'Save configuration' }).click()

  await expect.poll(() => requestBody).toBeTruthy()
  expect(requestBody?.name).toBe('Edge router')
  await expect(page.getByText('Edge router', { exact: true })).toBeVisible()
})

test('displays the authenticated user name in the topbar', async ({ page }) => {
  await mockBridge(page, (action) => {
    if (action === 'ListArtifacts') return { artifacts: [] }
    return []
  })

  await page.goto('/')
  await expect(page.locator('.user-badge')).toContainText('Operator Shawn')
})

test('updates an existing configuration through the update route', async ({ page }) => {
  let updatedBody: { id: string; name: string } | undefined

  await mockBridge(page, (action, request) => {
    if (action === 'ListArtifacts') return { artifacts: [] }
    if (action === 'ListConfigurations') {
      return [
        {
          id: 'config-1',
          name: 'Core Switch',
          targetAddress: '10.0.0.1',
          targetPort: 161,
          credentialReference: 'snmp-creds',
          timeoutMilliseconds: 5000,
          retries: 2,
          logLevel: 1,
          maximumWalkVariables: 100000,
          concurrentWalkWorkers: 4,
          useGetBulk: true,
          bulkMaxRepetitions: 25,
          partitionRecommendationBindings: 1000,
          getBulkDiagnosticOid: '',
          discoveryRoots: '1.3.6.1.1;1.3.6.1.2',
        },
      ]
    }
    if (action === 'UpdateConfiguration') {
      updatedBody = request as { id: string; name: string }
      return updatedBody
    }
    throw new Error(`Unexpected bridge action: ${action}`)
  })

  await page.goto('/')
  await page.getByRole('button', { name: 'Configurations' }).click()
  await page.getByRole('button', { name: /Core Switch/ }).click()

  const nameInput = page.getByLabel('Name')
  await expect(nameInput).toHaveValue('Core Switch')
  await nameInput.fill('Core Switch Updated')
  await page.getByRole('button', { name: 'Update configuration' }).click()

  await expect.poll(() => updatedBody).toBeTruthy()
  expect(updatedBody?.id).toBe('config-1')
  expect(updatedBody?.name).toBe('Core Switch Updated')
  await expect(page.getByRole('button', { name: /Core Switch Updated/ })).toBeVisible()
})

test('deletes a configuration after confirmation', async ({ page }) => {
  let deletedId: string | undefined

  await mockBridge(page, (action, request) => {
    if (action === 'ListArtifacts') return { artifacts: [] }
    if (action === 'ListConfigurations') {
      return [
        {
          id: 'config-to-delete',
          name: 'Old Router',
          targetAddress: '10.0.0.2',
          targetPort: 161,
          credentialReference: 'snmp-creds',
          timeoutMilliseconds: 5000,
          retries: 2,
          logLevel: 1,
          maximumWalkVariables: 100000,
          concurrentWalkWorkers: 4,
          useGetBulk: true,
          bulkMaxRepetitions: 25,
          partitionRecommendationBindings: 1000,
          getBulkDiagnosticOid: '',
          discoveryRoots: '1.3.6.1.1',
        },
      ]
    }
    if (action === 'DeleteConfiguration') {
      deletedId = (request as { id: string }).id
      return { success: true }
    }
    throw new Error(`Unexpected bridge action: ${action}`)
  })

  page.on('dialog', (dialog) => dialog.accept())

  await page.goto('/')
  await page.getByRole('button', { name: 'Configurations' }).click()
  await page.getByRole('button', { name: /Old Router/ }).click()
  await page.getByRole('button', { name: 'Delete configuration' }).click()

  await expect.poll(() => deletedId).toBe('config-to-delete')
  await expect(page.getByText('No saved walk configurations.')).toBeVisible()
})

test('redirects to DataMiner auth when session cookie is missing', async ({ page }) => {
  let redirectedUrl = ''
  await page.route('**/auth/**', (route) => {
    redirectedUrl = route.request().url()
    return route.fulfill({ status: 200, contentType: 'text/html', body: '<html><body>Login</body></html>' })
  })

  await page.goto('/')
  await expect.poll(() => redirectedUrl).toContain('/auth/?url=')
})

test('deletes a walk run artifact from the overview page after confirmation', async ({ page }) => {
  let deletedArtifactId: string | undefined

  await mockBridge(page, (action, request) => {
    if (action === 'ListArtifacts') return artifacts
    if (action === 'ListConfigurations') return []
    if (action === 'GetArtifactTree') return { oid: 'root', label: 'Observed OIDs', bindings: 1, children: [] }
    if (action === 'DeleteArtifact') {
      deletedArtifactId = (request as { artifactId: string }).artifactId
      return { success: true }
    }
    throw new Error(`Unexpected bridge action: ${action}`)
  })

  page.on('dialog', (dialog) => dialog.accept())

  await page.goto('/')
  const firstRunButton = page.locator('.run-item').filter({ hasText: '192.0.2.10' })
  await expect(firstRunButton).toBeVisible()

  await page.getByRole('button', { name: 'Delete walk' }).click()

  await expect.poll(() => deletedArtifactId).toBe('first-run')
  await expect(firstRunButton).not.toBeVisible()
  await expect(page.locator('.run-item').filter({ hasText: '192.0.2.11' })).toBeVisible()
})

test('dispatches walk execution from the configurations page with community string', async ({ page }) => {
  let executionRequest: Record<string, unknown> | undefined

  await mockBridge(page, (action, request) => {
    if (action === 'ListArtifacts') return { artifacts: [] }
    if (action === 'ListConfigurations') {
      return [
        {
          id: 'cfg-switch',
          name: 'Core Switch',
          targetAddress: '10.0.0.1',
          targetPort: 161,
          credentialReference: 'community-secret',
          timeoutMilliseconds: 5000,
          retries: 2,
          logLevel: 1,
          maximumWalkVariables: 100000,
          concurrentWalkWorkers: 4,
          useGetBulk: true,
          bulkMaxRepetitions: 25,
          partitionRecommendationBindings: 1000,
          getBulkDiagnosticOid: '',
          discoveryRoots: '1.3.6.1.1',
        },
      ]
    }
    if (action === 'ExecuteWalk') {
      executionRequest = request as Record<string, unknown>
      return { success: true, correlationId: 'corr-12345' }
    }
    throw new Error(`Unexpected bridge action: ${action}`)
  })

  page.on('dialog', (dialog) => {
    if (dialog.type() === 'prompt') {
      void dialog.accept('custom-community-val')
    }
  })

  await page.goto('/')
  await page.getByRole('button', { name: 'Configurations' }).click()
  await page.getByRole('button', { name: /Core Switch/ }).click()

  await page.getByRole('button', { name: 'Execute walk' }).click()

  await expect.poll(() => executionRequest).toBeTruthy()
  expect(executionRequest?.targetAddress).toBe('10.0.0.1')
  expect(executionRequest?.snmpCommunity).toBe('custom-community-val')
  await expect(page.getByText(/Walk dispatched in background on DMA/)).toBeVisible()
})