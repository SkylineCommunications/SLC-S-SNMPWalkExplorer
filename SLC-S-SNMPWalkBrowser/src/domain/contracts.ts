export type TerminalState =
  | 'outside-subtree'
  | 'end-of-mib'
  | 'request-failed'
  | 'global-safety-cap'

export interface RootOutcome {
  rootOid: string
  bindingCount: number
  lastOid: string | null
  terminalState: TerminalState
  isComplete: boolean
  partitionRecommended: boolean
  retryCount: number
  error?: string
}

export interface WalkArtifact {
  id: string
  rawFileName: string
  startedAtUtc: string
  completedAtUtc: string
  targetAddress: string
  targetPort: number
  snmpVersion: string
  concurrentWalkWorkers: number
  useGetBulk: boolean
  totalBindings: number
  isComplete: boolean
  rootOutcomes: RootOutcome[]
}

export interface Binding {
  oid: string
  value: string
}

export interface TreeNode {
  oid: string
  label: string
  bindings: number
  value?: string
  children?: TreeNode[]
}
