// Content script on DeviantArt's editor: offers to add the set's picks #2 and up, which DeviantArt's API can't take.
// DeviantArt's class names are scrambled, so only stable attributes are used: the extra-media file input (several
// files, images and video, unlike the one-image "Replace image" input) and the file names its thumbnails show.
;(async () => {
  const stashItemId = new URLSearchParams(location.search).get('deviationid')
  if (!stashItemId) return
  const extra = await chrome.runtime.sendMessage({ type: 'extra-picks', stashItemId })
  if (!extra || extra.picks.length === 0) return

  const host = document.createElement('div')
  host.style.cssText = 'all: initial; position: fixed; left: 1.25rem; bottom: 1.25rem; z-index: 2147483646;'
  const root = host.attachShadow({ mode: 'closed' })
  root.innerHTML = `
    <style>
      .box { display: flex; align-items: center; gap: 0.75rem; padding: 0.6rem 0.9rem; border-radius: 10px;
        background: #1c1d22; color: #e8e8ec; border: 1px solid #34353d; border-left: 12px solid #8b7cf6;
        box-shadow: 0 8px 24px rgb(0 0 0 / 0.45); font: 14px/1.4 system-ui, sans-serif; }
      .box.done { border-left-color: #6fd49a; }
      .box.failed { border-left-color: #f27474; }
      .title { font-weight: 600; }
      button { all: unset; cursor: pointer; padding: 0.35rem 0.8rem; border-radius: 6px; background: #8b7cf6; color: #fff;
        font-weight: 600; }
      button:hover { background: #7565f0; }
      button[disabled] { opacity: 0.6; cursor: default; }
      button[hidden] { display: none; }
    </style>
    <div class="box" role="status">
      <span><span class="title">Art Studio:</span> <span class="message"></span></span>
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

  function show(text, state, action) {
    message.textContent = text
    box.classList.toggle('done', state === 'done')
    box.classList.toggle('failed', state === 'failed')
    button.hidden = !action
    if (action) button.textContent = action
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
  new MutationObserver(refresh).observe(document.documentElement, { childList: true, subtree: true })
})()
