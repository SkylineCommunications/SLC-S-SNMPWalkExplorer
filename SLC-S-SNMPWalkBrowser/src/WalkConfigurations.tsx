import { useEffect, useState } from 'react'
import { Save, Server, Settings2, Trash2 } from 'lucide-react'
import { createConfiguration, deleteConfiguration, listConfigurations, updateConfiguration } from './api/walkApi'
import type { WalkConfiguration } from './api/walkApi'

const roots = ['1.3.6.1.1', '1.3.6.1.2', '1.3.6.1.3', '1.3.6.1.4', '1.3.6.1.5', '1.3.6.1.6', '1.3.6.1.7']

function emptyConfiguration(): WalkConfiguration {
  return {
    name: '', targetAddress: '', targetPort: 161, credentialReference: '', timeoutMilliseconds: 5000, retries: 2, logLevel: 1,
    maximumWalkVariables: 100000, concurrentWalkWorkers: 4, useGetBulk: true, bulkMaxRepetitions: 25,
    partitionRecommendationBindings: 1000, getBulkDiagnosticOid: '', discoveryRoots: roots.join(';'),
  }
}

export function WalkConfigurations() {
  const [configurations, setConfigurations] = useState<WalkConfiguration[]>([])
  const [configuration, setConfiguration] = useState<WalkConfiguration>(emptyConfiguration)
  const [selectedRoots, setSelectedRoots] = useState<Set<string>>(new Set(roots))
  const [error, setError] = useState('')
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    void listConfigurations().then(setConfigurations).catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Unable to load saved configurations.'))
  }, [])

  function setValue<Key extends keyof WalkConfiguration>(key: Key, value: WalkConfiguration[Key]) {
    setConfiguration((current) => ({ ...current, [key]: value }))
  }

  function loadConfiguration(saved: WalkConfiguration) {
    setConfiguration(saved)
    setSelectedRoots(new Set(saved.discoveryRoots.split(';').filter(Boolean)))
    setError('')
  }

  function toggleRoot(root: string) {
    setSelectedRoots((current) => {
      const next = new Set(current)
      if (next.has(root)) {
        next.delete(root)
      } else {
        next.add(root)
      }

      setConfiguration((saved) => ({ ...saved, discoveryRoots: roots.filter((item) => next.has(item)).join(';') }))
      return next
    })
  }

  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    setIsSaving(true)
    try {
      const saved = configuration.id
        ? await updateConfiguration(configuration)
        : await createConfiguration(configuration)
      setConfigurations((current) => {
        const without = current.filter((item) => item.id !== saved.id)
        return [...without, saved].sort((left, right) => left.name.localeCompare(right.name))
      })
      loadConfiguration(saved)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to save the configuration.')
    } finally {
      setIsSaving(false)
    }
  }

  async function handleDelete() {
    if (!configuration.id) return
    if (!window.confirm(`Are you sure you want to delete configuration "${configuration.name}"?`)) return
    setError('')
    setIsSaving(true)
    try {
      await deleteConfiguration(configuration.id)
      setConfigurations((current) => current.filter((item) => item.id !== configuration.id))
      setConfiguration(emptyConfiguration())
      setSelectedRoots(new Set(roots))
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to delete the configuration.')
    } finally {
      setIsSaving(false)
    }
  }

  return <section className="configuration-page">
    <aside className="configuration-list">
      <div className="panel-heading"><div><span className="eyebrow">Saved configurations</span><h1>Walk targets</h1></div><span className="count-chip">{configurations.length}</span></div>
      <div className="run-stack">
        {configurations.map((saved) => <button key={saved.id} type="button" className={`run-item ${saved.id === configuration.id ? 'selected' : ''}`} onClick={() => loadConfiguration(saved)}><div className="run-topline"><Server size={14} /><strong>{saved.name}</strong></div><span>{saved.targetAddress}:{saved.targetPort}</span><small>{saved.credentialReference}</small></button>)}
        {configurations.length === 0 && !error && <p>No saved walk configurations.</p>}
      </div>
    </aside>
    <section className="content configuration-content">
      <div className="page-heading"><div><span className="eyebrow">Walk configuration</span><h2>{configuration.id ? 'Edit configuration' : 'Define a collection'}</h2><p>Credential values remain in the DataMiner Credentials Library.</p></div><Settings2 size={28} aria-hidden="true" /></div>
      <form className="configuration-form panel" onSubmit={(event) => void save(event)}>
        <div className="section-heading"><div><span className="eyebrow">Connection</span><h3>Target and credentials</h3></div></div>
        <div className="form-grid">
          <label>Name<input required value={configuration.name} onChange={(event) => setValue('name', event.target.value)} /></label>
          <label>Target address<input required value={configuration.targetAddress} onChange={(event) => setValue('targetAddress', event.target.value)} /></label>
          <label>UDP port<input required min="1" max="65535" type="number" value={configuration.targetPort} onChange={(event) => setValue('targetPort', Number(event.target.value))} /></label>
          <label>Credential reference<input required value={configuration.credentialReference} onChange={(event) => setValue('credentialReference', event.target.value)} /></label>
        </div>
        <div className="section-heading form-section"><div><span className="eyebrow">Collection behavior</span><h3>Walk settings</h3></div></div>
        <div className="form-grid">
          <label>Timeout (ms)<input min="1" type="number" value={configuration.timeoutMilliseconds} onChange={(event) => setValue('timeoutMilliseconds', Number(event.target.value))} /></label>
          <label>Retries<input min="0" type="number" value={configuration.retries} onChange={(event) => setValue('retries', Number(event.target.value))} /></label>
          <label>Log level<input min="0" max="3" type="number" value={configuration.logLevel} onChange={(event) => setValue('logLevel', Number(event.target.value))} /></label>
          <label>Maximum variables<input min="1" type="number" value={configuration.maximumWalkVariables} onChange={(event) => setValue('maximumWalkVariables', Number(event.target.value))} /></label>
          <label>Concurrent workers<input min="1" type="number" value={configuration.concurrentWalkWorkers} onChange={(event) => setValue('concurrentWalkWorkers', Number(event.target.value))} /></label>
          <label>Bulk repetitions<input min="1" type="number" value={configuration.bulkMaxRepetitions} onChange={(event) => setValue('bulkMaxRepetitions', Number(event.target.value))} /></label>
          <label>Recommendation bindings<input min="1" type="number" value={configuration.partitionRecommendationBindings} onChange={(event) => setValue('partitionRecommendationBindings', Number(event.target.value))} /></label>
          <label>Diagnostic OID<input value={configuration.getBulkDiagnosticOid} onChange={(event) => setValue('getBulkDiagnosticOid', event.target.value)} /></label>
          <label className="checkbox-label"><input type="checkbox" checked={configuration.useGetBulk} onChange={(event) => setValue('useGetBulk', event.target.checked)} /> Use GETBULK</label>
        </div>
        <div className="section-heading form-section"><div><span className="eyebrow">Discovery scope</span><h3>Roots</h3></div></div>
        <div className="root-options">{roots.map((root) => <label key={root} className="checkbox-label"><input type="checkbox" checked={selectedRoots.has(root)} onChange={() => toggleRoot(root)} /> {root}</label>)}</div>
        {error && <p className="form-error" role="alert">{error}</p>}
        <div className="form-actions">
          <button type="button" onClick={() => { setConfiguration(emptyConfiguration()); setSelectedRoots(new Set(roots)); setError('') }}>New configuration</button>
          {configuration.id && (
            <button className="delete-button" type="button" disabled={isSaving} onClick={() => void handleDelete()}>
              <Trash2 size={17} /> Delete configuration
            </button>
          )}
          <button className="search-button" disabled={isSaving} type="submit">
            <Save size={17} /> {isSaving ? 'Saving...' : configuration.id ? 'Update configuration' : 'Save configuration'}
          </button>
        </div>
      </form>
    </section>
  </section>
}