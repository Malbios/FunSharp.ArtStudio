const puppeteer = require('puppeteer-extra')
const StealthPlugin = require('puppeteer-extra-plugin-stealth')

puppeteer.use(StealthPlugin())

async function main() {

	const errorMessage = `❌ Please provide the stash and inspiration urls\n${process.argv.toString()}`

	if (process.argv.length < 4) {
		console.error(errorMessage)
		process.exit(1)
	}

	const stashUrl = process.argv[2]
	const inspirationUrl = process.argv[3]

	if (!stashUrl) {
		console.error(errorMessage)
		process.exit(1)
	}

	if (!inspirationUrl) {
		console.error(errorMessage)
		process.exit(1)
	}

	const config = {
		headless: true,
		executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
		userDataDir: 'C:/dev/fsharp/FunSharp.ArtStudio/Utilities/puppeteer/ManualChromeProfiles',
		args: [
			'--start-maximized',
			`--user-data-dir='C:/dev/fsharp/FunSharp.ArtStudio/Utilities/puppeteer/ManualChromeProfiles'`,
			`--profile-dir='Profile 2'`
		]
	}

	const browser = await puppeteer.launch(config)
	const page = await browser.newPage()

	await page.setUserAgent('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36')

	await page.goto(stashUrl, { waitUntil: 'networkidle2' })

	// Focus the editor
	await page.focus('.editor-wrapper [contenteditable="true"]')

	// Move cursor to the end of content
	await page.keyboard.press('End')

	// Optionally, add a space or newline if needed
	await page.keyboard.type('Inspired by ')

	// Paste text at the cursor position
	await page.evaluate(() => navigator.clipboard.writeText(inspirationUrl))
	await page.keyboard.down('Control')
	await page.keyboard.press('KeyV')
	await page.keyboard.up('Control')

	await page.waitForSelector('button[aria-label="Alignment"]', { visible: true })
	await page.click('button[aria-label="Alignment"]')

	// Wait for and click the "Left" alignment button
	await page.waitForSelector('button:has(.c3jrxx.hHkqSB)') // Wait for the container
	await page.evaluate(() => {
		const buttons = [...document.querySelectorAll('button')]
		const leftButton = buttons.find(btn => btn.innerText.trim() === 'Left')
		leftButton?.click()
	})

	// Wait for and click the "Save and Exit" button
	await page.waitForSelector('button span.fwwM1f')
	await page.evaluate(() => {
		const saveButton = [...document.querySelectorAll('button')].find(btn =>
			btn.innerText.trim() === 'Save and Exit'
		)
		saveButton?.click()
	})

	console.log('ok')

	await browser.close()
}

async function withTimeout(promise, ms) {
	let timeout
	const timer = new Promise((_, reject) => {
		timeout = setTimeout(() => reject(new Error(`Timed out after ${ms}ms`)), ms)
	})
	const result = await Promise.race([promise, timer])
	clearTimeout(timeout)
	return result
}

(async () => {
	try {
		await withTimeout(main(), 5_000_000)
	} catch (err) {
		console.error('❌ Timeout or error:', err.message)
		process.exit(1)
	}
})()