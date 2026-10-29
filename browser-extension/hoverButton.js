// Content script on DeviantArt: a "+AS" button over the post image under the mouse, while Art Studio runs.
// It sits in its own overlay instead of inside DeviantArt's elements, so neither side's scripts or styles interfere.
;(async () => {
  const { isDeviantArtPost } = await import(chrome.runtime.getURL('deviationLink.js'))
  const MIN_IMAGE_SIZE = 120
  const INSET_PX = 8

  let { artStudioRunning: running = false, askForContext = false } = await chrome.storage.local.get([
    'artStudioRunning',
    'askForContext',
  ])
  let target = null
  let pointer = null
  let frame = 0

  const host = document.createElement('div')
  host.style.cssText = 'all: initial; position: fixed; z-index: 2147483646; display: none;'
  const root = host.attachShadow({ mode: 'closed' })
  root.innerHTML = `
    <style>
      button { all: unset; cursor: pointer; padding: 0.3rem 0.6rem; border-radius: 6px; background: #8b7cf6; color: #fff;
        font: 600 13px/1.2 system-ui, sans-serif; box-shadow: 0 2px 8px rgb(0 0 0 / 0.5); }
      button:hover { background: #7565f0; }
    </style>
    <button type="button"></button>`
  const button = root.querySelector('button')
  document.documentElement.append(host)
  showMode()

  function showMode() {
    button.textContent = askForContext ? '+AS…' : '+AS'
    button.title = askForContext ? 'Add to Art Studio with context' : 'Add to Art Studio'
  }

  const isLargeEnough = (image) => {
    const rect = image.getBoundingClientRect()
    return rect.width >= MIN_IMAGE_SIZE && rect.height >= MIN_IMAGE_SIZE
  }

  const contains = (element, { x, y }) => {
    const rect = element.getBoundingClientRect()
    return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom
  }

  const postLinkOf = (element) => {
    const link = element.closest('a[href]')
    return link && isDeviantArtPost(link.href) ? link : null
  }

  // Some layouts put the post link beside the image, or let the image ignore the mouse, so the image is looked for
  // around the post link under the mouse too.
  function imageNear(link, point) {
    let container = link
    for (let level = 0; level < 4 && container; level++, container = container.parentElement) {
      const image = [...container.querySelectorAll('img')].find((img) => contains(img, point) && isLargeEnough(img))
      if (image) return image
    }
    return null
  }

  /** The post under the mouse: the post a thumbnail's link opens, or the page's own post for its unlinked image. */
  function postAt(point, underMouse) {
    const directImage = underMouse.find((element) => element instanceof HTMLImageElement)
    const link = underMouse.map(postLinkOf).find(Boolean)
    if (link) {
      const image = directImage && isLargeEnough(directImage) ? directImage : imageNear(link, point)
      return image ? { image, linkUrl: link.href } : null
    }
    if (!directImage || !isLargeEnough(directImage) || directImage.closest('a[href]')) return null
    return isDeviantArtPost(location.href) ? { image: directImage, linkUrl: undefined } : null
  }

  function hide() {
    target = null
    host.style.display = 'none'
  }

  function place() {
    const rect = target.image.getBoundingClientRect()
    host.style.display = 'block'
    host.style.top = `${rect.top + INSET_PX}px`
    host.style.left = `${rect.left + INSET_PX}px`
  }

  // DeviantArt lays title overlays over its thumbnails, so look through everything under the mouse for the image.
  function update() {
    frame = 0
    if (!running || !pointer) return hide()
    const underMouse = document.elementsFromPoint(pointer.x, pointer.y)
    if (underMouse[0] === host) return
    const post = postAt(pointer, underMouse)
    if (!post) return hide()
    target = post
    place()
  }

  function scheduleUpdate() {
    if (!frame) frame = requestAnimationFrame(update)
  }

  document.addEventListener('mousemove', (event) => {
    pointer = { x: event.clientX, y: event.clientY }
    scheduleUpdate()
  }, { passive: true })
  document.addEventListener('scroll', scheduleUpdate, { capture: true, passive: true })
  window.addEventListener('resize', scheduleUpdate)
  document.documentElement.addEventListener('mouseleave', () => {
    pointer = null
    hide()
  })

  button.addEventListener('click', (event) => {
    event.preventDefault()
    event.stopPropagation()
    if (!target) return
    void chrome.runtime.sendMessage({ type: 'add', linkUrl: target.linkUrl, pageUrl: location.href })
  })

  chrome.storage.onChanged.addListener((changes) => {
    if (changes.artStudioRunning) running = changes.artStudioRunning.newValue === true
    if (changes.askForContext) {
      askForContext = changes.askForContext.newValue === true
      showMode()
    }
    scheduleUpdate()
  })
})()
