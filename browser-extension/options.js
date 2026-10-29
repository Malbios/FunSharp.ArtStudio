const input = document.getElementById('address')
const status = document.getElementById('status')

const { address } = await chrome.storage.local.get('address')
input.value = address ?? ''

document.getElementById('save').addEventListener('click', async () => {
  await chrome.storage.local.set({ address: input.value.trim() })
  status.textContent = input.value.trim() ? `Saved: ${input.value.trim()}` : 'Saved: back to http://localhost:5180'
})
