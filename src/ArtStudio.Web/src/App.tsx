import { BrowserRouter, NavLink, Route, Routes, useLocation } from 'react-router-dom'
import { api } from './api'
import { useStudioEvents } from './live/studioHub'
import { useLoad } from './live/useLoad'
import { NewSetPage } from './newset/NewSetPage'
import { QueuePage } from './queue/QueuePage'
import { SetDetailPage } from './sets/SetDetailPage'
import { SetsPage } from './sets/SetsPage'

function NewSetRoute() {
  const location = useLocation()
  return <NewSetPage key={location.search} />
}

function QueueNavLink() {
  const queue = useLoad(api.queue)
  useStudioEvents(['JobUpdated', 'QueueStateChanged'], queue.reload)
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
          <QueueNavLink />
          <NavLink to="/sets">Sets</NavLink>
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<NewSetRoute />} />
          <Route path="/queue" element={<QueuePage />} />
          <Route path="/sets" element={<SetsPage />} />
          <Route path="/sets/:id" element={<SetDetailPage />} />
        </Routes>
      </main>
    </BrowserRouter>
  )
}
