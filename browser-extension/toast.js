/**
 * Runs inside the DeviantArt tab (injected through chrome.scripting), so it must not use anything from outside its
 * own body. Shows the result of adding a post in the bottom right corner for a few seconds.
 */
export function showToast({ message, failed, openUrl, openLabel }) {
  const VISIBLE_MS = 6000
  const host = document.createElement('div')
  host.style.all = 'initial'
  const root = host.attachShadow({ mode: 'closed' })
  root.innerHTML = `
    <style>
      .toast { position: fixed; right: 1.25rem; bottom: 1.25rem; z-index: 2147483647; max-width: 26rem;
        display: flex; align-items: center; gap: 0.75rem; padding: 0.75rem 1rem; border-radius: 10px;
        background: #1c1d22; color: #e8e8ec; border: 1px solid #34353d; border-left: 4px solid #8b7cf6;
        box-shadow: 0 8px 24px rgb(0 0 0 / 0.45); font: 14px/1.4 system-ui, sans-serif;
        transition: opacity 0.3s ease; }
      .toast.failed { border-left-color: #f0616d; }
      .title { font-weight: 600; margin-right: 0.25rem; }
      a { color: #b3a8ff; white-space: nowrap; }
      button { all: unset; cursor: pointer; color: #9a9ba6; padding: 0 0.25rem; }
    </style>
    <div class="toast" role="status">
      <span><span class="title">Art Studio:</span><span class="message"></span></span>
      <a target="_blank" rel="noreferrer"></a>
      <button type="button" aria-label="Close">✕</button>
    </div>`

  const toast = root.querySelector('.toast')
  toast.classList.toggle('failed', failed)
  root.querySelector('.message').textContent = message
  const link = root.querySelector('a')
  if (openUrl) {
    link.href = openUrl
    link.textContent = openLabel
  } else {
    link.remove()
  }

  function close() {
    toast.style.opacity = '0'
    setTimeout(() => host.remove(), 300)
  }
  root.querySelector('button').addEventListener('click', close)
  let timer = setTimeout(close, VISIBLE_MS)
  toast.addEventListener('mouseenter', () => clearTimeout(timer))
  toast.addEventListener('mouseleave', () => (timer = setTimeout(close, VISIBLE_MS)))

  document.querySelectorAll('[data-art-studio-toast]').forEach((previous) => previous.remove())
  host.dataset.artStudioToast = ''
  document.documentElement.append(host)
}
