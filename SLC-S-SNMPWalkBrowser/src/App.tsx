import { useEffect, useState } from 'react'
import { ChevronDown, ChevronRight, CircleAlert, Download, FileSearch, Network, Search, Server, ShieldCheck, User } from 'lucide-react'
import { getConnectionId, getCurrentUser, startKeepAlive } from './api/auth'
import type { KeepAliveStatus } from './api/auth'
import { downloadRawArtifact, getTree, listArtifacts, searchBindings } from './api/walkApi'
import type { Binding, TreeNode, WalkArtifact } from './domain/contracts'
import { WalkConfigurations } from './WalkConfigurations'
import './App.css'

function formatNumber(value: number) {
  return new Intl.NumberFormat('en-US').format(value)
}

function formatTime(value: string) {
  return new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

interface TreeBranchProps {
  node: TreeNode
  depth?: number
  expandedOids: Set<string>
  selectedOid: string
  onToggle: (oid: string) => void
}

interface ArtifactTree {
  artifactId: string
  value: TreeNode
}

function TreeBranch({ node, depth = 0, expandedOids, selectedOid, onToggle }: TreeBranchProps) {
  const hasChildren = Boolean(node.children?.length)
  const isExpanded = expandedOids.has(node.oid)
  const isSelected = node.oid === selectedOid

  return (
    <li>
      <button className={`tree-row ${isSelected ? 'tree-selected' : ''}`} type="button" style={{ paddingLeft: `${depth * 18}px` }} onClick={() => hasChildren && onToggle(node.oid)}>
        {hasChildren ? isExpanded ? <ChevronDown size={15} aria-hidden="true" /> : <ChevronRight size={15} aria-hidden="true" /> : <span className="tree-leaf" />}
        <span className="tree-label">{node.label}</span>
        <span className="tree-oid">{node.oid}</span>
        <span className="tree-count" title="Bindings beneath this OID">{formatNumber(node.bindings)} bindings</span>
      </button>
      {!hasChildren && node.value && <div className={`tree-value ${isSelected ? 'tree-value-selected' : ''}`} style={{ paddingLeft: `${depth * 18 + 18}px` }}><span className="tree-value-label">Value</span><span>{node.value}</span></div>}
      {hasChildren && isExpanded && <ul>{node.children?.map((child) => <TreeBranch key={child.oid} node={child} depth={depth + 1} expandedOids={expandedOids} selectedOid={selectedOid} onToggle={onToggle} />)}</ul>}
    </li>
  )
}

function App() {
  const [currentUser] = useState(() => getCurrentUser())
  const [keepAliveStatus, setKeepAliveStatus] = useState<KeepAliveStatus>('connecting')
  const [artifacts, setArtifacts] = useState<WalkArtifact[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [treeResult, setTreeResult] = useState<ArtifactTree | null>(null)
  const [prefix, setPrefix] = useState('1.3.6.1')
  const [query, setQuery] = useState('')
  const [page, setPage] = useState<'overview' | 'explorer' | 'configurations'>('overview')
  const [expandedOids, setExpandedOids] = useState<Set<string>>(new Set())
  const [selectedOid, setSelectedOid] = useState('')
  const [matchCount, setMatchCount] = useState(0)
  const [loadError, setLoadError] = useState('')
  const [treeLoadError, setTreeLoadError] = useState('')

  useEffect(() => {
    const connectionId = getConnectionId()
    if (!connectionId) return
    const stop = startKeepAlive(connectionId, setKeepAliveStatus)
    return () => stop()
  }, [])

  useEffect(() => {
    void listArtifacts().then((items) => {
      setArtifacts(items)
      setSelectedId(items[0]?.id ?? '')
    }).catch((error: unknown) => {
      setLoadError(error instanceof Error ? error.message : 'Unable to load SNMP walk artifacts.')
    })
  }, [])

  useEffect(() => {
    if (!selectedId) {
      return
    }

    let isCurrent = true
    void getTree(selectedId).then((nextTree) => {
      if (isCurrent) {
        setTreeResult({ artifactId: selectedId, value: nextTree })
        setTreeLoadError('')
      }
    }).catch((error: unknown) => {
      if (isCurrent) {
        setTreeLoadError(error instanceof Error ? error.message : 'Unable to load the OID hierarchy.')
      }
    })

    return () => {
      isCurrent = false
    }
  }, [selectedId])

  const selected = artifacts.find((artifact) => artifact.id === selectedId)
  const tree = treeResult?.artifactId === selectedId ? treeResult.value : null

  async function handleSearch() {
    if (!selected) {
      return
    }

    try {
      const matches = await searchBindings(selected.id, prefix, query)
      setMatchCount(matches.length)
      if (matches[0]) {
        selectBinding(matches[0])
      } else {
        setSelectedOid('')
      }
    } catch (error) {
      setLoadError(error instanceof Error ? error.message : 'Unable to search SNMP walk bindings.')
    }
  }

  function toggleOid(oid: string) {
    setExpandedOids((current) => {
      const next = new Set(current)
      if (next.has(oid)) {
        next.delete(oid)
      } else {
        next.add(oid)
      }
      return next
    })
  }

  function selectArtifact(artifactId: string) {
    setSelectedId(artifactId)
    setExpandedOids(new Set())
    setSelectedOid('')
    setTreeLoadError('')
  }

  function selectBinding(binding: Binding) {
    const arcs = binding.oid.split('.')
    const ancestors = arcs.slice(0, -1).map((_, index) => arcs.slice(0, index + 1).join('.'))
    setExpandedOids(new Set(['root', ...ancestors]))
    setSelectedOid(binding.oid)
  }

  function setTreeExpansion(expanded: boolean) {
    if (!tree || !expanded) {
      setExpandedOids(new Set())
      return
    }

    const branchOids = new Set<string>()
    function collectBranches(node: TreeNode) {
      if (node.children?.length) {
        branchOids.add(node.oid)
        node.children.forEach(collectBranches)
      }
    }

    collectBranches(tree)
    setExpandedOids(branchOids)
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <div className="brand"><Network size={23} strokeWidth={2.4} /><span>SNMP Walk Browser</span></div>
        <nav className="main-nav" aria-label="Primary navigation"><button className={page === 'overview' ? 'active' : ''} type="button" onClick={() => setPage('overview')}>Overview</button><button className={page === 'explorer' ? 'active' : ''} type="button" onClick={() => setPage('explorer')}>Explorer</button><button className={page === 'configurations' ? 'active' : ''} type="button" onClick={() => setPage('configurations')}>Configurations</button></nav>
        <div className="connection">
          <span className={`connection-status-dot ${keepAliveStatus}`} title={`DMS Session: ${keepAliveStatus}`} />
          <ShieldCheck size={16} /> Read-only evidence workspace
          {currentUser && (
            <div className="user-badge" title="Authenticated DataMiner User">
              <User size={14} />
              <span>{currentUser.name}</span>
            </div>
          )}
        </div>
      </header>
      {page === 'configurations' ? <WalkConfigurations /> : <section className="workspace">
        <aside className="run-list">
          <div className="panel-heading"><div><span className="eyebrow">Evidence runs</span><h1>Completed walks</h1></div><span className="count-chip">{artifacts.length}</span></div>
          <div className="run-stack">
            {artifacts.map((artifact) => <button className={`run-item ${artifact.id === selectedId ? 'selected' : ''}`} onClick={() => selectArtifact(artifact.id)} key={artifact.id} type="button">
              <div className="run-topline"><span className={artifact.isComplete ? 'status-dot healthy' : 'status-dot partial'} /> <strong>{artifact.targetAddress}</strong><span>:{artifact.targetPort}</span></div>
              <span>{formatTime(artifact.completedAtUtc)}</span>
              <small>{formatNumber(artifact.totalBindings)} bindings · {artifact.snmpVersion}</small>
            </button>)}
            {loadError && <p role="alert">{loadError}</p>}
            {!loadError && artifacts.length === 0 && <p>No completed walk artifacts are available.</p>}
          </div>
        </aside>
        {selected && page === 'overview' && <section className="content">
          <div className="page-heading"><div><span className="eyebrow">Collection evidence</span><h2>{selected.targetAddress}:{selected.targetPort}</h2><p>{selected.rawFileName}</p></div><div className="tree-actions"><button type="button" title="Download raw walk evidence" onClick={() => void downloadRawArtifact(selected.id, selected.rawFileName)}><Download size={15} aria-hidden="true" /> Download raw walk</button><span className={`state-badge ${selected.isComplete ? 'complete' : 'partial'}`}>{selected.isComplete ? 'Complete' : 'Partial'}</span></div></div>
          <section className="stats-grid">
            <div className="stat"><Server size={18} /><span>Protocol</span><strong>{selected.snmpVersion}</strong></div>
            <div className="stat"><FileSearch size={18} /><span>Bindings</span><strong>{formatNumber(selected.totalBindings)}</strong></div>
            <div className="stat"><Network size={18} /><span>Workers</span><strong>{selected.concurrentWalkWorkers} GETNEXT</strong></div>
            <div className="stat"><ShieldCheck size={18} /><span>Published</span><strong>{formatTime(selected.completedAtUtc)}</strong></div>
          </section>
          <section className="panel health-panel"><div className="section-heading"><div><span className="eyebrow">Prefix health</span><h3>Root outcomes</h3></div><span className="muted">Metadata commit marker verified</span></div><div className="table-wrap"><table><thead><tr><th>Root</th><th>State</th><th>Bindings</th><th>Retries</th><th>Last OID</th><th>Notes</th></tr></thead><tbody>{selected.rootOutcomes.map((outcome) => <tr key={outcome.rootOid}><td className="mono">{outcome.rootOid}</td><td><span className={`terminal ${outcome.isComplete ? 'ok' : 'error'}`}>{outcome.terminalState}</span></td><td>{formatNumber(outcome.bindingCount)}</td><td>{outcome.retryCount}</td><td className="mono muted">{outcome.lastOid ?? '—'}</td><td>{outcome.partitionRecommended ? <span className="recommendation">Partition recommended</span> : outcome.error ? <span className="error-note" title={outcome.error}><CircleAlert size={15} /> Request failure</span> : '—'}</td></tr>)}</tbody></table></div></section>
        </section>}
        {selected && page === 'explorer' && <section className="content explorer-content">
          <div className="page-heading"><div><span className="eyebrow">Explorer</span><h2>Search and hierarchy</h2><p>Results expand and focus their matching OID in the observed tree.</p></div><span className="state-badge partial">{formatNumber(selected.totalBindings)} bindings</span></div>
          <section className="panel search-panel explorer-search"><div className="section-heading"><div><span className="eyebrow">Binding query</span><h3>Search retained data</h3></div><span className="muted">{matchCount > 0 ? `${matchCount} match${matchCount === 1 ? '' : 'es'}` : 'Bounded result set'}</span></div><div className="search-controls"><label>OID prefix<input value={prefix} onChange={(event) => setPrefix(event.target.value)} inputMode="decimal" /></label><label>Value contains<input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="e.g. GigabitEthernet" /></label><button className="search-button" type="button" onClick={() => void handleSearch()}><Search size={17} /> Search bindings</button></div></section>
          <section className="panel tree-panel explorer-tree"><div className="section-heading"><div><span className="eyebrow">OID navigation</span><h3>Observed hierarchy</h3></div><div className="tree-actions"><span className="muted">{selectedOid ? `Focused: ${selectedOid}` : 'Initially collapsed'}</span><button type="button" onClick={() => setTreeExpansion(true)}>Expand all</button><button type="button" onClick={() => setTreeExpansion(false)}>Collapse all</button></div></div>{tree ? <><div className="tree-columns"><span>Description</span><span>OID</span><span>Descendant bindings</span></div><ul className="tree"><TreeBranch node={tree} expandedOids={expandedOids} selectedOid={selectedOid} onToggle={toggleOid} /></ul></> : treeLoadError ? <p role="alert">{treeLoadError}</p> : <p>No valid bindings are available for this walk artifact.</p>}</section>
        </section>}
      </section>}
    </main>
  )
}

export default App
