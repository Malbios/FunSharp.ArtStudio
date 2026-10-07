import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import { BlockedArtistsEditor } from './BlockedArtistsEditor'
import { BuildingBlocksEditor } from './BuildingBlocksEditor'
import { GeneralSettings } from './GeneralSettings'

export function SettingsPage() {
  const { hash } = useLocation()

  useEffect(() => {
    if (hash) document.getElementById(hash.slice(1))?.scrollIntoView()
  }, [hash])

  return (
    <div className="settings-sections">
      <h2>Settings</h2>
      <GeneralSettings />
      <BuildingBlocksEditor />
      <BlockedArtistsEditor />
    </div>
  )
}
