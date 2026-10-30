// Content script on DeviantArt's editor: offers to add the set's picks #2 and up, which DeviantArt's API can't take.
// DeviantArt's class names are scrambled, so only stable attributes are used: the extra-media file input (several
// files, images and video, unlike the one-image "Replace image" input) and the file names its thumbnails show.
;(async () => {
  const stashItemId = new URLSearchParams(location.search).get('deviationid')
  if (!stashItemId) return
  const extra = await chrome.runtime.sendMessage({ type: 'extra-picks', stashItemId })
  if (!extra || extra.picks.length === 0) return

  const EDGE_GAP = 20
  const CLEARANCE = 12
  const CONTROLS = 'button, a[href], input, select, textarea, [role=button]'

  const host = document.createElement('div')
  host.style.cssText = `all: initial; position: fixed; right: ${EDGE_GAP}px; bottom: ${EDGE_GAP}px; z-index: 2147483646;`
  const root = host.attachShadow({ mode: 'closed' })
  root.innerHTML = `
    <style>
      .box { box-sizing: border-box; width: 10rem; display: flex; flex-direction: column; gap: 0.5rem;
        padding: 0.6rem 0.8rem; border-radius: 10px; background: #1c1d22; color: #e8e8ec; border: 1px solid #34353d;
        border-left: 12px solid #8b7cf6; box-shadow: 0 8px 24px rgb(0 0 0 / 0.45); font: 14px/1.4 system-ui, sans-serif; }
      .box.done { border-left-color: #6fd49a; }
      .box.failed { border-left-color: #f27474; }
      .title { font-weight: 600; }
      .message::first-letter { text-transform: uppercase; }
      button { all: unset; display: block; text-align: center; cursor: pointer; padding: 0.35rem 0.5rem; border-radius: 6px;
        background: #8b7cf6; color: #fff; font-weight: 600; }
      button:hover { background: #7565f0; }
      button[disabled] { opacity: 0.6; cursor: default; }
      button[hidden] { display: none; }
    </style>
    <div class="box" role="status">
      <div class="title">Art Studio</div>
      <div class="message"></div>
      <button type="button"></button>
    </div>`
  const box = root.querySelector('.box')
  const message = root.querySelector('.message')
  const button = root.querySelector('button')
  const first = extra.picks[0].position
  const last = extra.picks.at(-1).position
  const range = first === last ? `pick #${first}` : `picks #${first}–#${last}`
  let busy = false

  const extraMediaInput = () =>
    [...document.querySelectorAll('input[type=file]')].find((input) => input.multiple && input.accept.includes('video/'))
  const inEditor = () => new Set([...document.querySelectorAll('img[alt]')].map((img) => img.alt))
  const missingPicks = () => {
    const present = inEditor()
    return extra.picks.filter((pick) => !present.has(pick.fileName))
  }

  const scrollsItself = (element) => {
    const { overflowY } = getComputedStyle(element)
    return (overflowY === 'auto' || overflowY === 'scroll') && element.scrollHeight > element.clientHeight
  }

  // Content scrolling past underneath is left alone, so the box doesn't jump around while scrolling.
  function staysOnScreen(control) {
    for (let element = control; element && element !== document.documentElement; element = element.parentElement) {
      const { position } = getComputedStyle(element)
      if (position === 'fixed' || position === 'sticky') return true
      if (scrollsItself(element)) return false
    }
    return document.documentElement.scrollHeight <= innerHeight
  }

  // Stays at the bottom and moves left of DeviantArt's controls there, such as the submit buttons.
  function place() {
    const { width, height } = host.getBoundingClientRect()
    const top = innerHeight - EDGE_GAP - height - CLEARANCE
    const controls = [...document.querySelectorAll(CONTROLS)]
      .map((control) => ({ control, rect: control.getBoundingClientRect() }))
      .filter(({ rect }) => rect.width > 0 && rect.height > 0 && rect.bottom > top)
    const pinned = new Map()
    const isPinned = (control) => {
      if (!pinned.has(control)) pinned.set(control, staysOnScreen(control))
      return pinned.get(control)
    }

    let right = innerWidth - EDGE_GAP
    for (;;) {
      const left = right - width
      const blocking = controls.filter(
        ({ control, rect }) => rect.right > left - CLEARANCE && rect.left < right + CLEARANCE && isPinned(control),
      )
      if (blocking.length === 0 || left <= EDGE_GAP) break
      right = Math.min(...blocking.map(({ rect }) => rect.left)) - CLEARANCE
    }
    host.style.right = `${innerWidth - Math.max(right, width + EDGE_GAP)}px`
  }

  let placing = false
  function schedulePlace() {
    if (placing) return
    placing = true
    requestAnimationFrame(() => {
      placing = false
      place()
    })
  }

  function show(text, state, action) {
    message.textContent = text
    box.classList.toggle('done', state === 'done')
    box.classList.toggle('failed', state === 'failed')
    button.hidden = !action
    if (action) button.textContent = action
    schedulePlace()
  }

  function refresh() {
    if (busy) return
    const missing = missingPicks()
    if (missing.length === 0) return show(`${range} ${first === last ? 'is' : 'are'} in the editor.`, 'done')
    if (!extraMediaInput()) return show("couldn't find DeviantArt's upload control for more images.", 'failed')
    const label = missing.length === 1 ? `Add 1 image (#${missing[0].position})` : `Add ${missing.length} images`
    show(range, 'ready', label)
  }

  button.addEventListener('click', async () => {
    const input = extraMediaInput()
    const missing = missingPicks()
    if (!input || missing.length === 0) return refresh()
    busy = true
    button.disabled = true
    show(`fetching ${missing.length === 1 ? '1 image' : `${missing.length} images`}…`, 'ready', 'Adding…')
    const answer = await chrome.runtime.sendMessage({ type: 'extra-pick-files', picks: missing })
    busy = false
    button.disabled = false
    if (!answer || answer.error) return show(answer?.error ?? 'Art Studio did not answer.', 'failed', 'Try again')

    const transfer = new DataTransfer()
    for (const { fileName, type, base64 } of answer.files) {
      const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0))
      transfer.items.add(new File([bytes], fileName, { type }))
    }
    input.files = transfer.files
    input.dispatchEvent(new Event('input', { bubbles: true }))
    input.dispatchEvent(new Event('change', { bubbles: true }))
    show(`handed ${answer.files.length === 1 ? '1 image' : `${answer.files.length} images`} to DeviantArt; uploading…`, 'ready')
  })

  document.documentElement.append(host)
  refresh()
  new MutationObserver(() => {
    refresh()
    schedulePlace()
  }).observe(document.documentElement, { childList: true, subtree: true })
  addEventListener('resize', schedulePlace)
})()
