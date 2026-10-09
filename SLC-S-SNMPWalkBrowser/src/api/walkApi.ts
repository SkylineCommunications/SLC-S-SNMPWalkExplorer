import type { Binding, TreeNode, WalkArtifact } from '../domain/contracts'

export interface WalkConfiguration {
  id?: string
  name: string
  targetAddress: string
  targetPort: number
  credentialReference: string
  timeoutMilliseconds: number
  retries: number
  logLevel: number
  maximumWalkVariables: number
  concurrentWalkWorkers: number
  useGetBulk: boolean
  bulkMaxRepetitions: number
  partitionRecommendationBindings: number
  getBulkDiagnosticOid: string
  discoveryRoots: string
}

const apiBaseUrl = (import.meta.env.VITE_WALK_API_BASE_URL ?? '/api/v1/custom/snmp-walk-explorer').replace(/\/$/, '')

interface ArtifactListResponse {
  artifacts: WalkArtifact[]
}

export async function listArtifacts(): Promise<WalkArtifact[]> {
  if (!apiBaseUrl) {
    throw new Error('The SNMP walk API base URL is not configured.')
  }

  const response = await fetch(`${apiBaseUrl}/artifacts`, { credentials: 'same-origin' })
  if (!response.ok) {
    throw new Error(`The SNMP walk API returned ${response.status}.`)
  }

  const payload: ArtifactListResponse = await response.json()
  return payload.artifacts
}

export async function getArtifact(id: string): Promise<WalkArtifact | undefined> {
  return (await listArtifacts()).find((artifact) => artifact.id === id)
}

export async function downloadRawArtifact(id: string, fileName: string): Promise<void> {
  if (!apiBaseUrl) {
    throw new Error('The SNMP walk API base URL is not configured.')
  }

  const response = await fetch(`${apiBaseUrl}/artifacts/${encodeURIComponent(id)}/raw`, { credentials: 'same-origin' })
  if (!response.ok) {
    throw new Error(`The SNMP walk API returned ${response.status}.`)
  }

  const downloadUrl = URL.createObjectURL(new Blob([await response.text()], { type: 'application/x-ndjson' }))
  const link = document.createElement('a')
  link.href = downloadUrl
  link.download = fileName
  link.click()
  URL.revokeObjectURL(downloadUrl)
}

export async function searchBindings(artifactId: string, prefix: string, query: string): Promise<Binding[]> {
  if (!apiBaseUrl) {
    throw new Error('The SNMP walk API base URL is not configured.')
  }

  const searchParameters = new URLSearchParams()
  if (prefix) searchParameters.set('prefix', prefix)
  if (query) searchParameters.set('query', query)
  const response = await fetch(`${apiBaseUrl}/artifacts/${encodeURIComponent(artifactId)}/bindings?${searchParameters}`, { credentials: 'same-origin' })
  if (!response.ok) {
    throw new Error(`The SNMP walk API returned ${response.status}.`)
  }

  const payload: { bindings: Binding[] } = await response.json()
  return payload.bindings
}

export async function listConfigurations(): Promise<WalkConfiguration[]> {
  if (!apiBaseUrl) {
    throw new Error('The SNMP walk API base URL is not configured.')
  }

  const response = await fetch(`${apiBaseUrl}/configs`, { credentials: 'same-origin' })
  if (!response.ok) {
    throw new Error(`The SNMP walk API returned ${response.status}.`)
  }

  return response.json()
}

export async function createConfiguration(configuration: WalkConfiguration): Promise<WalkConfiguration> {
  if (!apiBaseUrl) {
    throw new Error('The SNMP walk API base URL is not configured.')
  }

  const response = await fetch(`${apiBaseUrl}/configs/create`, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(configuration),
  })
  if (!response.ok) {
    throw new Error(await response.text() || `The SNMP walk API returned ${response.status}.`)
  }

  return response.json()
}

export async function getTree(artifactId: string): Promise<TreeNode> {
  if (!apiBaseUrl) {
    throw new Error('The SNMP walk API base URL is not configured.')
  }

  const response = await fetch(`${apiBaseUrl}/artifacts/${encodeURIComponent(artifactId)}/tree`, { credentials: 'same-origin' })
  if (!response.ok) {
    throw new Error(`The SNMP walk API returned ${response.status}.`)
  }

  return response.json()
}
