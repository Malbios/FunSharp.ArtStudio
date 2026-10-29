import { showContextDialog } from './contextDialog.js'
import { postUrlFrom } from './deviationLink.js'
import { showToast } from './toast.js'

const DEFAULT_ADDRESS = 'http://localhost:5180'
// Chrome puts a second entry of the same extension into a submenu, so one entry serves both modes.
const ADD = 'add'
const CHECK_ALARM = 'check-art-studio'
const CHECK_TIMEOUT_MS = 2000

async function artStudioAddress() {
  const { address } = await chrome.storage.local.get('address')
  return (address || DEFAULT_ADDRESS).replace(/\/+$/, '')
}

async function asksForContext() {
  const { askForContext } = await chrome.storage.local.get('askForContext')
  return askForContext === true
}

function createMenus() {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({
      id: ADD,
      title: 'Add to Art Studio',
      contexts: ['image', 'link'],
      documentUrlPatterns: ['*://*.deviantart.com/*'],
      visible: false,
    })
    void showMode()
    void checkArtStudio()
  })
  // 30 seconds is the shortest interval Chrome allows.
  chrome.alarms.create(CHECK_ALARM, { periodInMinutes: 0.5 })
}

/** The menu entry, toolbar badge and tooltip say whether adding asks for context first. */
async function showMode() {
  const ask = await asksForContext()
  await chrome.contextMenus.update(ADD, { title: ask ? 'Add to Art Studio with context…' : 'Add to Art Studio' }).catch(() => {})
  await chrome.action.setBadgeText({ text: ask ? 'CTX' : '' })
  await chrome.action.setBadgeBackgroundColor({ color: '#8b7cf6' })
  await chrome.action.setTitle({
    title: ask
      ? 'Art Studio: asks for context before adding. Click to add right away instead.'
      : 'Art Studio: adds right away. Click to ask for context first.',
  })
}

/**
 * Shows the menu entry only while Art Studio answers, since Chrome can't ask right when the menu opens. The same
 * request brings the posts Art Studio has and the users it refuses, so the hover button can tell from links alone.
 */
async function checkArtStudio() {
  const known = await fetchKnownDeviations(await artStudioAddress())
  const running = known !== null
  await chrome.storage.local.set({ artStudioRunning: running, ...(running && { known }) })
  try {
    await chrome.contextMenus.update(ADD, { visible: running })
  } catch {
    // The menu is being created again; that run checks once it exists.
  }
}

async function fetchKnownDeviations(address) {
  try {
    const response = await fetch(`${address}/api/deviantart/known`, { signal: AbortSignal.timeout(CHECK_TIMEOUT_MS) })
    return response.ok ? await response.json() : null
  } catch {
    return null
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
  if (!url) return report(tab, { message: "That isn't a DeviantArt post.", failed: true })

  const address = await artStudioAddress()
  try {
    let visionContext
    if (await asksForContext()) {
      await showOnPage(tab, { message: 'Checking the link…', failed: false, pending: true })
      const preview = await post(address, '/api/deviantart/preview', { url })
      await clearReport(tab)
      const context = await askForContext(tab.id, preview)
      if (context === null) return
      visionContext = context || undefined
    }
    await showOnPage(tab, { message: 'Adding…', failed: false, pending: true })
    const { id } = await post(address, '/api/drafts/deviantart', { url, visionContext })
    await report(tab, { message: `Added as draft #${id}.`, failed: false, openUrl: `${address}/?draft=${id}`, openLabel: 'Open draft' })
  } catch (failure) {
    const existingSetId = failure instanceof ArtStudioError ? failure.existingSetId : null
    await report(tab, {
      message: failure.message,
      failed: true,
      openUrl: existingSetId === null ? undefined : `${address}/sets/${existingSetId}`,
      openLabel: `Open set #${existingSetId}`,
    })
  }
  await checkArtStudio()
}

/** Shows the result on the page, where it can't be missed; a system notification only where the page can't show it. */
async function report(tab, result) {
  if (!(await showOnPage(tab, result))) await notify(result)
}

async function showOnPage(tab, result) {
  try {
    await chrome.scripting.executeScript({ target: { tabId: tab.id }, func: showToast, args: [result] })
    return true
  } catch {
    return false
  }
}

async function clearReport(tab) {
  await chrome.scripting
    .executeScript({
      target: { tabId: tab.id },
      func: () => document.querySelectorAll('[data-art-studio-toast]').forEach((toast) => toast.remove()),
    })
    .catch(() => {})
}

async function notify({ message, openUrl }) {
  const id = await chrome.notifications.create({ type: 'basic', iconUrl: 'icons/icon128.png', title: 'Art Studio', message })
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
  if (changes.askForContext) void showMode()
})
chrome.action.onClicked.addListener(async () => {
  await chrome.storage.local.set({ askForContext: !(await asksForContext()) })
})
chrome.contextMenus.onClicked.addListener((info, tab) => void addToArtStudio(info, tab))
/** The editor's content script can't reach localhost itself, so the files pass through here as base64. */
async function extraPicks(stashItemId) {
  const address = await artStudioAddress()
  const response = await fetch(`${address}/api/stash/${encodeURIComponent(stashItemId)}/extra-picks`).catch(() => null)
  return response?.ok ? await response.json() : null
}

async function extraPickFiles(picks) {
  const address = await artStudioAddress()
  return Promise.all(
    picks.map(async ({ fileName, url }) => {
      const response = await fetch(`${address}${url}`)
      if (!response.ok) throw new Error(`Art Studio could not send ${fileName} (${response.status}).`)
      const bytes = new Uint8Array(await response.arrayBuffer())
      let binary = ''
      for (let start = 0; start < bytes.length; start += 0x8000) binary += String.fromCharCode(...bytes.subarray(start, start + 0x8000))
      return { fileName, type: response.headers.get('Content-Type') ?? 'image/png', base64: btoa(binary) }
    }),
  )
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === 'extra-picks') {
    void extraPicks(message.stashItemId).then(sendResponse)
    return true
  }
  if (message?.type === 'extra-pick-files') {
    extraPickFiles(message.picks).then(
      (files) => sendResponse({ files }),
      (failure) => sendResponse({ error: failure.message }),
    )
    return true
  }
})
chrome.runtime.onMessage.addListener((message, sender) => {
  if (message?.type === 'add' && sender.tab)
    void addToArtStudio({ linkUrl: message.linkUrl, pageUrl: message.pageUrl }, sender.tab)
  if (message?.type === 'open-set' && Number.isInteger(message.setId))
    void artStudioAddress().then((address) => chrome.tabs.create({ url: `${address}/sets/${message.setId}` }))
})
chrome.notifications.onClicked.addListener(async (id) => {
  const key = `notification:${id}`
  const { [key]: openUrl } = await chrome.storage.session.get(key)
  if (openUrl) await chrome.tabs.create({ url: openUrl })
  await chrome.storage.session.remove(key)
  chrome.notifications.clear(id)
})

// Lets the service worker's DevTools console (and automated checks) run a click without the native menu.
self.artStudio = { addToArtStudio, checkArtStudio }
