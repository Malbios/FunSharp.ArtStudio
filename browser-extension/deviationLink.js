const POST_PATHS = [
  /^\/[A-Za-z0-9_-]+\/art\/(?:[^/]*-)?\d+\/?$/,
  /^\/art\/(?:[^/]*-)?\d+\/?$/,
  /^\/deviation\/\d+\/?$/,
]

export function isDeviantArtPost(url) {
  let parsed
  try {
    parsed = new URL(url)
  } catch {
    return false
  }
  const host = parsed.hostname.toLowerCase()
  if (host !== 'deviantart.com' && !host.endsWith('.deviantart.com')) return false
  return POST_PATHS.some((path) => path.test(decodeURIComponent(parsed.pathname)))
}

/** The post a right-click was about: the link around the clicked thumbnail, otherwise the post page itself. */
export function postUrlFrom(linkUrl, pageUrl) {
  return [linkUrl, pageUrl].find((url) => url && isDeviantArtPost(url)) ?? null
}
