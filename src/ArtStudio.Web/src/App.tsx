import { BrowserRouter, NavLink, Route, Routes, useLocation } from 'react-router-dom'
import { api } from './api'
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

function DraftsNavLink() {
  const drafts = useLoad(() => api.sets('draft'))
  useStudioEvents(['SetUpdated', 'SetDeleted'], drafts.reload)
  const count = drafts.data?.length ?? 0

  return (
    <NavLink to="/drafts">
      Drafts
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
          <DraftsNavLink />
          <QueueNavLink />
          <NavLink to="/sets">Sets</NavLink>
          <NavLink to="/post">Post</NavLink>
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
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </main>
    </BrowserRouter>
  )
}
