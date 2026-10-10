import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import { api } from '../api'
import { useLoad } from '../live/useLoad'
import { BlockedArtistsEditor } from './BlockedArtistsEditor'
import { BuildingBlocksEditor } from './BuildingBlocksEditor'
import { BundlesEditor } from './BundlesEditor'
import { DeviantArtConnection } from './DeviantArtConnection'
import { GeneralSettings } from './GeneralSettings'
import { PresetsEditor } from './PresetsEditor'
import { VisionSettings } from './VisionSettings'

export function SettingsPage() {
  const { hash } = useLocation()
  const blocks = useLoad(api.buildingBlocks)

  useEffect(() => {
    if (hash) document.getElementById(hash.slice(1))?.scrollIntoView()
  }, [hash])

  return (
    <div className="settings-sections">
      <h2>Settings</h2>
      <GeneralSettings />
      <DeviantArtConnection />
      <VisionSettings />
      <PresetsEditor blocks={blocks} />
      <BundlesEditor blocks={blocks} />
      <BuildingBlocksEditor />
      <BlockedArtistsEditor />
    </div>
  )
}
