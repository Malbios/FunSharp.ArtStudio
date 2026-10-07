import { BrowserRouter, NavLink, Route, Routes, useLocation } from 'react-router-dom'
import { NewSetPage } from './newset/NewSetPage'

function NewSetRoute() {
  const location = useLocation()
  return <NewSetPage key={location.search} />
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
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<NewSetRoute />} />
        </Routes>
      </main>
    </BrowserRouter>
  )
}
