import type { Binding, TreeNode, WalkArtifact } from '../domain/contracts'
import { executeBridge } from './dataMinerAutomationApi'

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

export interface WalkExecutionRequest {
  targetAddress: string
  targetPort?: number
  snmpCommunity?: string
  timeoutMilliseconds?: number
  retries?: number
  logLevel?: number
  maximumWalkVariables?: number
  concurrentWalkWorkers?: number
  useGetBulk?: boolean
  bulkMaxRepetitions?: number
  partitionRecommendationBindings?: number
  getBulkDiagnosticOid?: string
  discoveryRoots?: string
  runCorrelationId?: string
}

export interface WalkExecutionResponse {
  success: boolean
  correlationId: string
}

interface ArtifactListResponse {
  artifacts: WalkArtifact[]
}

export async function listArtifacts(): Promise<WalkArtifact[]> {
  const payload = await executeBridge<ArtifactListResponse>('ListArtifacts')
  return payload.artifacts
}

export async function getArtifact(id: string): Promise<WalkArtifact | undefined> {
  return (await listArtifacts()).find((artifact) => artifact.id === id)
}

export async function downloadRawArtifact(id: string, fileName: string): Promise<void> {
  const rawArtifact = await executeBridge<string>('DownloadArtifact', { artifactId: id })
  const downloadUrl = URL.createObjectURL(new Blob([rawArtifact], { type: 'application/x-ndjson' }))
  const link = document.createElement('a')
  link.href = downloadUrl
  link.download = fileName
  link.click()
  URL.revokeObjectURL(downloadUrl)
}

export async function searchBindings(artifactId: string, prefix: string, query: string): Promise<Binding[]> {
  const payload = await executeBridge<{ bindings: Binding[] }>('SearchBindings', { artifactId, prefix, query })
  return payload.bindings
}

export async function listConfigurations(): Promise<WalkConfiguration[]> {
  return executeBridge<WalkConfiguration[]>('ListConfigurations')
}

export async function createConfiguration(configuration: WalkConfiguration): Promise<WalkConfiguration> {
  return executeBridge<WalkConfiguration>('CreateConfiguration', configuration)
}

export async function updateConfiguration(configuration: WalkConfiguration): Promise<WalkConfiguration> {
  return executeBridge<WalkConfiguration>('UpdateConfiguration', configuration)
}

export async function deleteConfiguration(id: string): Promise<void> {
  await executeBridge<{ success: boolean }>('DeleteConfiguration', { id })
}

export async function deleteArtifact(id: string): Promise<void> {
  await executeBridge<{ success: boolean }>('DeleteArtifact', { artifactId: id })
}

export async function executeWalk(request: WalkExecutionRequest): Promise<WalkExecutionResponse> {
  return executeBridge<WalkExecutionResponse>('ExecuteWalk', request)
}

export async function getTree(artifactId: string): Promise<TreeNode> {
  return executeBridge<TreeNode>('GetArtifactTree', { artifactId })
}
