/**
 * Runs inside the DeviantArt tab (injected through chrome.scripting), so it must not use anything from outside its
 * own body. Resolves with the typed context, or null when cancelled.
 */
export function showContextDialog(preview) {
  const MAX_CONTEXT_LENGTH = 1000
  return new Promise((resolve) => {
    const host = document.createElement('div')
    host.style.all = 'initial'
    const root = host.attachShadow({ mode: 'closed' })
    root.innerHTML = `
      <style>
        dialog { width: min(56rem, calc(100vw - 2rem)); background: #1c1d22; color: #e8e8ec; border: 1px solid #34353d;
          border-radius: 10px; padding: 1.25rem; font: 14px/1.4 system-ui, sans-serif; }
        dialog::backdrop { background: rgb(0 0 0 / 0.6); }
        h3 { margin: 0 0 1rem; font-size: 1.05rem; }
        .body { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 1.25rem; margin-bottom: 1rem; }
        img { display: block; max-width: 100%; max-height: 60vh; margin: 0 auto; border-radius: 6px; }
        .muted { color: #9a9ba6; font-size: 0.85rem; text-align: center; overflow-wrap: anywhere; }
        textarea { box-sizing: border-box; width: 100%; height: 12rem; resize: vertical; background: #121317; color: inherit;
          border: 1px solid #34353d; border-radius: 6px; padding: 0.5rem; font: inherit; }
        textarea:focus { outline: 2px solid #8b7cf6; border-color: transparent; }
        .actions { display: flex; justify-content: flex-end; gap: 0.75rem; }
        button { font: inherit; padding: 0.45rem 1rem; border-radius: 6px; border: 1px solid #34353d; background: #26272e;
          color: inherit; cursor: pointer; }
        button.primary { background: #8b7cf6; border-color: #8b7cf6; color: #fff; }
        @media (max-width: 700px) { .body { grid-template-columns: minmax(0, 1fr); } img { max-height: 35vh; } }
      </style>
      <dialog>
        <form method="dialog">
          <h3>Add to Art Studio: additional context</h3>
          <div class="body">
            <div>
              <img alt="">
              <p class="muted caption"></p>
            </div>
            <label>
              <textarea placeholder="What the vision model should know about this image, e.g. who a person is"></textarea>
            </label>
          </div>
          <div class="actions">
            <button type="button" class="cancel">Cancel</button>
            <button type="submit" class="primary" title="Ctrl+Enter">OK</button>
          </div>
        </form>
      </dialog>`

    const dialog = root.querySelector('dialog')
    const textarea = root.querySelector('textarea')
    const image = preview.images?.[0]
    if (image) root.querySelector('img').src = image.imageUrl
    else root.querySelector('img').remove()
    root.querySelector('.caption').textContent = `${preview.title ?? 'Untitled'} by ${preview.author}`
    textarea.maxLength = MAX_CONTEXT_LENGTH

    function finish(result) {
      dialog.close()
      host.remove()
      resolve(result)
    }

    // Keeps DeviantArt's own keyboard shortcuts from reacting while typing.
    host.addEventListener('keydown', (event) => event.stopPropagation())
    textarea.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
        event.preventDefault()
        finish(textarea.value.trim())
      }
    })
    root.querySelector('form').addEventListener('submit', (event) => {
      event.preventDefault()
      finish(textarea.value.trim())
    })
    root.querySelector('.cancel').addEventListener('click', () => finish(null))
    dialog.addEventListener('cancel', (event) => {
      event.preventDefault()
      finish(null)
    })

    document.documentElement.append(host)
    dialog.showModal()
    textarea.focus()
  })
}
