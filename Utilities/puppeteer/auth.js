const puppeteer = require('puppeteer-extra')
const StealthPlugin = require('puppeteer-extra-plugin-stealth')

puppeteer.use(StealthPlugin())

async function waitAndClickLogin(page) {
	await page.waitForFunction(() => {
		const btns = Array.from(document.querySelectorAll('button'))
		return btns.some(b => (b.innerText || '').trim() === 'Log in')
	}, { timeout: 10000 })

	await page.evaluate(() => {
		const btn = Array.from(document.querySelectorAll('button'))
			.find(b => (b.innerText || '').trim() === 'Log in')
		if (btn) btn.click()
	})

	// console.log('✅ waitAndClick: clicked a login button')
}

async function waitAndClickGoogle(page) {
	await page.waitForFunction(
		() => {
			const btn = Array.from(document.querySelectorAll('button'))
				.find(b => (b.innerText || '').trim().includes('Continue with Google'))
			return btn && !btn.disabled && btn.offsetParent !== null
		}, { timeout: 10000 })

	await page.evaluate(() => {
		const btn = Array.from(document.querySelectorAll('button'))
			.find(b => (b.innerText || '').trim().includes('Continue with Google'))
		if (btn) btn.click()
	})

	// console.log('✅ waitAndClick: clicked a Google login button')
}

(async () => {
	const browser = await puppeteer.launch({ headless: true, args: [] })
	const page = await browser.newPage()
	await page.setUserAgent('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36')

	page.on('request', req => {
		const token = req.headers()['openai-sentinel-token'] || req.headers()['OpenAI-Sentinel-Token']
		if (token) {
			console.log(token)
			process.exit(0)
		}
	})

	await page.goto('https://sora.chatgpt.com', { waitUntil: 'networkidle2' })

	await waitAndClickLogin(page)

	await new Promise(resolve => setTimeout(resolve, 5000))

	await waitAndClickLogin(page)

	await new Promise(resolve => setTimeout(resolve, 5000))

	await waitAndClickGoogle(page)
})()

/*
<button class="inline-flex gap-1.5 items-center justify-center whitespace-nowrap text-sm font-semibold focus-visible:outline-none data-[disabled=true]:pointer-events-none data-[disabled=true]:cursor-default group/button relative bg-token-bg-inverse text-token-text-inverse hover:bg-token-bg-inverse/80 data-[disabled=true]:opacity-100 data-[disabled=true]:bg-token-bg-composer-button data-[disabled=true]:text-token-text-primary/40 px-3 py-2 h-9 rounded-full">Log in</button>

<button class="btn relative btn-blue btn-large" data-testid="login-button"><div class="flex items-center justify-center"><span>Log in</span></div></button>

<button class="_buttonStyleFix_kzpwf_64 _root_1yxsq_60 _leftAlign_1yxsq_91 _outline_1yxsq_103" aria-describedby="" aria-disabled="false" data-dd-action-name="Continue with Google" form="«r1»" name="intent" value="google"><div class="_decoration_1yxsq_129"><div class="_logoPositioner_kzpwf_50"><img width="18" height="18" alt="Google logo" class="_root_jbbqu_1" src="https://auth-cdn.oaistatic.com/assets/google-logo-NePEveMl.svg"></div></div>Continue with Google</button>
*/