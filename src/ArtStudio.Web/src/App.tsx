import { BrowserRouter, NavLink, Route, Routes, useLocation } from 'react-router-dom'
import { api } from './api'
import { ArchivePage } from './archive/ArchivePage'
import { DraftsPage } from './drafts/DraftsPage'
import { DesktopNotifications } from './live/desktopNotifications'
import { SettingsPage } from './settings/SettingsPage'
import { useStudioEvents } from './live/studioHub'
import { useLoad } from './live/useLoad'
import { NewSetPage } from './newset/NewSetPage'
import { PostPage } from './post/PostPage'
import { QueuePage } from './queue/QueuePage'
import { SetDetailPage } from './sets/SetDetailPage'
import { SetsPage } from './sets/SetsPage'

function NewSetRoute() {
  const location = useLocation()
  return <NewSetPage key={location.search} />
}

interface StageNavLinkProps {
  stage: 'draft' | 'working' | 'ready'
  to: string
  label: string
}

/** A nav link with the number of sets in its stage; queueing a draft or finishing a job can move sets between stages. */
function StageNavLink({ stage, to, label }: StageNavLinkProps) {
  const sets = useLoad(() => api.sets(stage))
  useStudioEvents(['SetUpdated', 'SetDeleted', 'JobUpdated'], sets.reload)
  const count = sets.data?.length ?? 0

  return (
    <NavLink to={to}>
      {label}
      {count > 0 && <span className="nav-count">{count}</span>}
    </NavLink>
  )
}

function QueueNavLink() {
  const queue = useLoad(api.queue)
  useStudioEvents(['JobUpdated', 'QueueStateChanged', 'SetDeleted'], queue.reload)
  const pending = queue.data?.active.filter((job) => job.status !== 'Failed').length ?? 0
  const failed = queue.data?.active.some((job) => job.status === 'Failed') ?? false

  return (
    <NavLink to="/queue">
      Queue
      {pending > 0 && <span className="nav-count">{pending}</span>}
      {failed && <span className="nav-count failed">!</span>}
      {queue.data?.paused && !failed && <span className="nav-count paused">paused</span>}
    </NavLink>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <header className="top-bar">
        <h1>Art Studio</h1>
        <nav>
          <NavLink to="/" end>
            New
          </NavLink>
          <StageNavLink stage="draft" to="/drafts" label="Drafts" />
          <QueueNavLink />
          <StageNavLink stage="working" to="/sets" label="Sets" />
          <StageNavLink stage="ready" to="/post" label="Post" />
          <NavLink to="/archive">Archive</NavLink>
          <NavLink to="/settings">Settings</NavLink>
        </nav>
      </header>
      <DesktopNotifications />
      <main>
        <Routes>
          <Route path="/" element={<NewSetRoute />} />
          <Route path="/drafts" element={<DraftsPage />} />
          <Route path="/queue" element={<QueuePage />} />
          <Route path="/sets" element={<SetsPage />} />
          <Route path="/sets/:id" element={<SetDetailPage />} />
          <Route path="/post" element={<PostPage />} />
          <Route path="/archive" element={<ArchivePage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </main>
    </BrowserRouter>
  )
}
