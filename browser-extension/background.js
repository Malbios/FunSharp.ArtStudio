import { showContextDialog } from './contextDialog.js'
import { postUrlFrom } from './deviationLink.js'

const DEFAULT_ADDRESS = 'http://localhost:5180'
const ADD = 'add'
const ADD_WITH_CONTEXT = 'add-with-context'
const CHECK_ALARM = 'check-art-studio'
const CHECK_TIMEOUT_MS = 2000

async function artStudioAddress() {
  const { address } = await chrome.storage.local.get('address')
  return (address || DEFAULT_ADDRESS).replace(/\/+$/, '')
}

function createMenus() {
  chrome.contextMenus.removeAll(() => {
    const shared = { contexts: ['image', 'link'], documentUrlPatterns: ['*://*.deviantart.com/*'], visible: false }
    chrome.contextMenus.create({ ...shared, id: ADD, title: 'Add to Art Studio' })
    chrome.contextMenus.create({ ...shared, id: ADD_WITH_CONTEXT, title: 'Add to Art Studio with context…' })
    void checkArtStudio()
  })
  // 30 seconds is the shortest interval Chrome allows.
  chrome.alarms.create(CHECK_ALARM, { periodInMinutes: 0.5 })
}

/** Shows the menu entries only while Art Studio answers, since Chrome can't ask right when the menu opens. */
async function checkArtStudio() {
  const running = await isArtStudioRunning(await artStudioAddress())
  for (const id of [ADD, ADD_WITH_CONTEXT]) {
    try {
      await chrome.contextMenus.update(id, { visible: running })
    } catch {
      // The menus are being created again; that run checks once they exist.
    }
  }
}

async function isArtStudioRunning(address) {
  try {
    const response = await fetch(`${address}/api/settings`, { signal: AbortSignal.timeout(CHECK_TIMEOUT_MS) })
    return response.ok
  } catch {
    return false
  }
}

class ArtStudioError extends Error {
  constructor(message, existingSetId = null) {
    super(message)
    this.existingSetId = existingSetId
  }
}

async function post(address, path, body) {
  let response
  try {
    response = await fetch(`${address}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    })
  } catch {
    throw new ArtStudioError(`Art Studio isn't running on ${address}.`)
  }
  const json = await response.json().catch(() => null)
  if (!response.ok) throw new ArtStudioError(json?.error ?? `Art Studio answered ${response.status}.`, json?.existingSetId ?? null)
  return json
}

async function askForContext(tabId, preview) {
  const [injection] = await chrome.scripting.executeScript({ target: { tabId }, func: showContextDialog, args: [preview] })
  return injection?.result ?? null
}

async function addToArtStudio(info, tab) {
  const url = postUrlFrom(info.linkUrl, info.pageUrl)
  if (!url) return notify("That isn't a DeviantArt post.")

  const address = await artStudioAddress()
  try {
    let visionContext
    if (info.menuItemId === ADD_WITH_CONTEXT) {
      const preview = await post(address, '/api/deviantart/preview', { url })
      const context = await askForContext(tab.id, preview)
      if (context === null) return
      visionContext = context || undefined
    }
    const { id } = await post(address, '/api/drafts/deviantart', { url, visionContext })
    await notify(`Added as draft #${id}.`, url, `${address}/?draft=${id}`)
  } catch (failure) {
    const existingSetId = failure instanceof ArtStudioError ? failure.existingSetId : null
    await notify(failure.message, url, existingSetId === null ? undefined : `${address}/sets/${existingSetId}`)
  }
}

async function notify(message, postUrl, openUrl) {
  const id = await chrome.notifications.create({
    type: 'basic',
    iconUrl: 'icons/icon128.png',
    title: 'Art Studio',
    message,
    contextMessage: postUrl ?? '',
  })
  if (openUrl) await chrome.storage.session.set({ [`notification:${id}`]: openUrl })
}

chrome.runtime.onInstalled.addListener(createMenus)
chrome.runtime.onStartup.addListener(createMenus)
chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === CHECK_ALARM) void checkArtStudio()
})
chrome.tabs.onActivated.addListener(() => void checkArtStudio())
chrome.tabs.onUpdated.addListener((_tabId, change) => {
  if (change.status === 'complete') void checkArtStudio()
})
chrome.storage.onChanged.addListener((changes) => {
  if (changes.address) void checkArtStudio()
})
chrome.contextMenus.onClicked.addListener((info, tab) => void addToArtStudio(info, tab))
chrome.notifications.onClicked.addListener(async (id) => {
  const key = `notification:${id}`
  const { [key]: openUrl } = await chrome.storage.session.get(key)
  if (openUrl) await chrome.tabs.create({ url: openUrl })
  await chrome.storage.session.remove(key)
  chrome.notifications.clear(id)
})

// Lets the service worker's DevTools console (and automated checks) run a click without the native menu.
self.artStudio = { addToArtStudio, checkArtStudio }
