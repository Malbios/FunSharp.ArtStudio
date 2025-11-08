const puppeteer = require('puppeteer-extra')
const StealthPlugin = require('puppeteer-extra-plugin-stealth')

puppeteer.use(StealthPlugin())

async function waitForAndClickMenuItem(page, selector, itemName) {

	await page.waitForSelector(selector, { visible: true })

	const found = await page.evaluate((selector, itemName) => {
		const el = Array
			.from(document.querySelectorAll(selector))
			.find(e => e.textContent.trim() === itemName)

		if (el) {
			el.click()
			return true
		}

		return false
	}, selector, itemName)

	if (!found)
		throw new Error('❌ menu item not found: ' + selector + ' -> ' + itemName)
}

async function main() {

	const errorMessage = `❌ Please provide the image file path as a command line argument.\n${process.argv.toString()}`

	if (process.argv.length < 3) {
		console.error(errorMessage)
		process.exit(1)
	}

	const imageFilePath = process.argv[2]
	if (!imageFilePath) {
		console.error(errorMessage)
		process.exit(1)
	}

	const config = {
		headless: true,
		executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
		userDataDir: 'C:/dev/fsharp/FunSharp.ArtStudio/Utilities/puppeteer/ChromeProfiles',
		args: [
			'--start-maximized',
			`--user-data-dir='C:/dev/fsharp/FunSharp.ArtStudio/Utilities/puppeteer/ChromeProfiles'`,
			`--profile-dir='Profile 2'`
		]
	}

	const browser = await puppeteer.launch(config)
	const page = await browser.newPage()

	await page.setUserAgent('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36')

	await page.goto('https://chatgpt.com/g/g-690dc426f0288191b94ac04161ccc658-image2prompt-converter', { waitUntil: 'networkidle2' })

	// Wait for file input and upload the image
	await page.waitForSelector('#upload-camera', { visible: false })
	const input = await page.$('#upload-camera')
	await input.uploadFile(imageFilePath)

	// Wait until the upload finishes and then click the send button
	await page.waitForSelector('#composer-submit-button:not([disabled])')
	await page.click('#composer-submit-button')

	// wait for thumbs up button to appear (response is done)
	await page.waitForSelector('[data-testid="good-response-turn-action-button"]', { visible: true, timeout: 120000 })

	// select the "..." top right and click it
	const prompt = await page.$eval('code.whitespace-pre\\! span', el => el.innerText)
	await page.click('[data-testid="conversation-options-button"]')

	await waitForAndClickMenuItem(page, 'div[role="menuitem"]', 'Delete')

	// confirm delete
	await page.waitForSelector('[data-testid="delete-conversation-confirm-button"]', { visible: true })
	await page.click('[data-testid="delete-conversation-confirm-button"]')

	await browser.close()

	console.log(prompt)
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
		await withTimeout(main(), 60_000) // 60 seconds
	} catch (err) {
		console.error('❌ Timeout or error:', err)
		process.exit(1)
	}
})()