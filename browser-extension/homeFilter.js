// Content script on DeviantArt: hides the home page's thumbnails of posts Art Studio already knows, when switched on.
// DeviantArt changes pages without reloading, so every page is watched and the home page is told by its address.
;(async () => {
  const { isHomePage, isKnown, parsePost, postState } = await import(chrome.runtime.getURL('deviationLink.js'))
  const HIDDEN = 'data-art-studio-hidden'
  const MAX_TILE_LEVELS = 6

  const settings = await chrome.storage.local.get(['artStudioRunning', 'known', 'hideKnownOnHome'])
  let frame = 0

  const style = document.createElement('style')
  style.textContent = `[${HIDDEN}] { display: none !important; }`
  document.documentElement.append(style)

  const holdsOtherPost = (element, id) =>
    [...element.querySelectorAll('a[href]')].some((link) => {
      const other = parsePost(link.href)
      return other !== null && other.id !== id
    })

  // DeviantArt's class names are scrambled, so a thumbnail's tile is the largest element around its link that holds
  // no other post.
  function tileOf(link, id) {
    let tile = link
    for (let level = 0; level < MAX_TILE_LEVELS; level++) {
      const parent = tile.parentElement
      if (!parent || parent === document.body || holdsOtherPost(parent, id)) break
      tile = parent
    }
    return tile
  }

  function apply() {
    frame = 0
    const active = settings.hideKnownOnHome === true && settings.artStudioRunning === true && isHomePage(location.href)
    const tiles = new Set()
    if (active) {
      for (const link of document.querySelectorAll('a[href]')) {
        const post = parsePost(link.href)
        if (post && isKnown(postState(post, settings.known))) tiles.add(tileOf(link, post.id))
      }
    }
    for (const element of document.querySelectorAll(`[${HIDDEN}]`)) if (!tiles.has(element)) element.removeAttribute(HIDDEN)
    for (const tile of tiles) if (!tile.hasAttribute(HIDDEN)) tile.setAttribute(HIDDEN, '')
  }

  function scheduleApply() {
    if (!frame) frame = requestAnimationFrame(apply)
  }

  chrome.storage.onChanged.addListener((changes) => {
    for (const key of ['artStudioRunning', 'known', 'hideKnownOnHome']) if (changes[key]) settings[key] = changes[key].newValue
    scheduleApply()
  })
  new MutationObserver(scheduleApply).observe(document.documentElement, { childList: true, subtree: true })
  scheduleApply()
})()
