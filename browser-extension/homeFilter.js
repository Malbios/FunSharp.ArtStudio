// Content script on DeviantArt: on the home page, borders the thumbnails of users who already inspired a set, and hides
// those of posts Art Studio already knows when switched on.
// DeviantArt changes pages without reloading, so every page is watched and the home page is told by its address.
;(async () => {
  const { inspirationLevel, isHomePage, isKnown, parsePost, postState } = await import(chrome.runtime.getURL('deviationLink.js'))
  const HIDDEN = 'data-art-studio-hidden'
  const INSPIRED = 'data-art-studio-inspired'
  const ANCHORED = 'data-art-studio-anchored'
  const BORDER_WIDTH = '3px'
  const BORDER_COLORS = { inspiring: '#f2c66d', published: '#ff00ff' }
  const STRIPE_WIDTH = '16px'
  const MAX_TILE_LEVELS = 6

  const SETTINGS = ['artStudioRunning', 'known', 'hideKnownOnHome', 'hideInspiringOnHome', 'hidePublishedOnHome']
  const settings = await chrome.storage.local.get(SETTINGS)
  let frame = 0

  // The tile's own outline would be covered by DeviantArt's positioned layers inside it, so the border is drawn on top.
  // Striped, because artists frame their own work in plain colors. The mask leaves only the border of the stripes.
  const style = document.createElement('style')
  style.textContent = [
    `[${HIDDEN}] { display: none !important; }`,
    `[${ANCHORED}] { position: relative !important; }`,
    `[${INSPIRED}]::after { content: ''; position: absolute; inset: 0; z-index: 1; pointer-events: none;
      border: ${BORDER_WIDTH} solid transparent;
      mask: linear-gradient(#000 0 0) padding-box exclude, linear-gradient(#000 0 0) border-box; }`,
    ...Object.entries(BORDER_COLORS).map(([level, color]) => `[${INSPIRED}="${level}"]::after {
      background: repeating-linear-gradient(45deg, ${color} 0 ${STRIPE_WIDTH}, #fff ${STRIPE_WIDTH} calc(2 * ${STRIPE_WIDTH})) border-box; }`),
  ].join('\n')
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
    const active = settings.artStudioRunning === true && isHomePage(location.href)
    const hidden = new Set()
    const inspired = new Map()
    if (active) {
      for (const link of document.querySelectorAll('a[href]')) {
        const post = parsePost(link.href)
        if (!post) continue
        const level = inspirationLevel(post, settings.known)
        const hides =
          (settings.hideKnownOnHome === true && isKnown(postState(post, settings.known))) ||
          (settings.hideInspiringOnHome === true && level !== null) ||
          (settings.hidePublishedOnHome === true && level === 'published')
        if (!hides && !level) continue
        const tile = tileOf(link, post.id)
        if (hides) hidden.add(tile)
        if (level) inspired.set(tile, level)
      }
    }
    for (const element of document.querySelectorAll(`[${HIDDEN}]`)) if (!hidden.has(element)) element.removeAttribute(HIDDEN)
    for (const tile of hidden) if (!tile.hasAttribute(HIDDEN)) tile.setAttribute(HIDDEN, '')
    for (const element of document.querySelectorAll(`[${INSPIRED}]`)) {
      if (inspired.has(element)) continue
      element.removeAttribute(INSPIRED)
      element.removeAttribute(ANCHORED)
    }
    for (const [tile, level] of inspired) {
      if (tile.getAttribute(INSPIRED) === level) continue
      if (getComputedStyle(tile).position === 'static') tile.setAttribute(ANCHORED, '')
      tile.setAttribute(INSPIRED, level)
    }
  }

  function scheduleApply() {
    if (!frame) frame = requestAnimationFrame(apply)
  }

  chrome.storage.onChanged.addListener((changes) => {
    for (const key of SETTINGS) if (changes[key]) settings[key] = changes[key].newValue
    scheduleApply()
  })
  new MutationObserver(scheduleApply).observe(document.documentElement, { childList: true, subtree: true })
  scheduleApply()
})()
