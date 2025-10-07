const puppeteer = require('puppeteer-extra')
const StealthPlugin = require('puppeteer-extra-plugin-stealth')

puppeteer.use(StealthPlugin())

async function main() {
	const config = {
		headless: true,
		executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
		userDataDir: 'C:/dev/fsharp/DeviantArt/Utilities/puppeteer/ChromeProfiles',
		args: [
			'--start-maximized',
			`--user-data-dir='C:/dev/fsharp/DeviantArt/Utilities/puppeteer/ChromeProfiles'`,
			`--profile-dir='Profile 2'`
		]
	}

	const browser = await puppeteer.launch(config)
	const page = await browser.newPage()

	await page.setUserAgent('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36')

	page.on('request', async req => {
		const sentinel = req.headers()['openai-sentinel-token'] || req.headers()['OpenAI-Sentinel-Token']

		if (sentinel) {
			const cookies = await page.cookies()
			const cookieHeader = cookies
				.map(c => `${c.name}=${c.value}`)
				.join('; ')

			console.log(JSON.stringify(sentinel))
			console.log(cookieHeader)
			process.exit(0)
		}
	})

	await page.goto('https://sora.chatgpt.com', { waitUntil: 'networkidle2' })
}

(async () => {
	await main()
})()