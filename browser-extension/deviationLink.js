const USER_ART_PATH = /^\/(?<user>[A-Za-z0-9_-]+)\/art\/(?:[^/]*-)?(?<id>\d+)\/?$/
const SUBDOMAIN_ART_PATH = /^\/art\/(?:[^/]*-)?(?<id>\d+)\/?$/
const DEVIATION_PATH = /^\/deviation\/(?<id>\d+)\/?$/

/** The post a DeviantArt link points to, as Art Studio reads it: its number, and its user when the link names one. */
export function parsePost(url) {
  let parsed
  try {
    parsed = new URL(url)
  } catch {
    return null
  }
  const host = parsed.hostname.toLowerCase()
  if (host !== 'deviantart.com' && !host.endsWith('.deviantart.com')) return null
  const path = decodeURIComponent(parsed.pathname)
  const subdomain = host.split('.')[0]

  const userArt = USER_ART_PATH.exec(path)
  if (userArt) return { id: userArt.groups.id, username: userArt.groups.user }
  const subdomainArt = SUBDOMAIN_ART_PATH.exec(path)
  if (subdomainArt && subdomain !== 'www' && subdomain !== 'deviantart') return { id: subdomainArt.groups.id, username: subdomain }
  const deviation = DEVIATION_PATH.exec(path)
  if (deviation) return { id: deviation.groups.id, username: null }
  return null
}

export const isDeviantArtPost = (url) => parsePost(url) !== null

/** The post a right-click was about: the link around the clicked thumbnail, otherwise the post page itself. */
export function postUrlFrom(linkUrl, pageUrl) {
  return [linkUrl, pageUrl].find((url) => url && isDeviantArtPost(url)) ?? null
}

export function isHomePage(url) {
  try {
    const parsed = new URL(url)
    return parsed.hostname.toLowerCase() === 'www.deviantart.com' && parsed.pathname === '/'
  } catch {
    return false
  }
}

/** Posts Art Studio has already, or wouldn't take. */
export const isKnown = (state) => state.kind !== 'add'

/** Whether Art Studio would take the post, from what it already has, without asking anyone. */
export function postState(post, known) {
  const setId = known?.usedDeviations?.[post.id]
  if (setId !== undefined) return { kind: 'used', setId }
  const username = post.username?.toLowerCase()
  if (username && known?.ownUsername?.toLowerCase() === username) return { kind: 'own' }
  if (username && known?.blockedUsers?.some((blocked) => blocked.toLowerCase() === username)) return { kind: 'blocked' }
  return { kind: 'add' }
}
