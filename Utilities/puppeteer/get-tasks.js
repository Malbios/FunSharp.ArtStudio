async function getTasks(sentinelToken, bearerToken, before) {
	return await fetch(`https://sora.chatgpt.com/backend/video_gen${before}`, {
		"headers": {
			"openai-sentinel-token": sentinelToken,
			"authorization": `Bearer ${bearerToken}`,
			"content-type": "application/json",
			"user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0",
			"accept": "*/*",
			"accept-language": "en-US,en;q=0.9,de;q=0.8",
			"cache-control": "no-cache",
			"pragma": "no-cache",
			"priority": "u=1, i",
			"sec-ch-ua": "\"Chromium\";v=\"140\", \"Not=A?Brand\";v=\"24\", \"Google Chrome\";v=\"140\"",
			"sec-ch-ua-mobile": "?0",
			"sec-ch-ua-platform": "\"Windows\"",
			"sec-fetch-dest": "empty",
			"sec-fetch-mode": "cors",
			"sec-fetch-site": "same-origin",
			"Referer": "https://sora.chatgpt.com/library/trash"
		},
		"body": null,
		"method": "GET"
	})
}

async function main() {
	try {
		const sentinelToken = JSON.parse(process.argv[2])
		const bearerToken = process.argv[3]

		let tasks = []
		let before = ""

		if (process.argv.length > 4)
			before = `?before=${process.argv[4]}`

		while (true) {
			let response = await getTasks(sentinelToken, bearerToken, before)
			let responseContent = await response.json()

			for (const task of responseContent.task_responses)
				tasks.push(task)

			if (!responseContent.has_more)
				break
			else
				before = `?before=${responseContent.task_responses.slice(-1)[0].id}`
		}

		console.log(JSON.stringify(tasks))
	} catch (err) {
		const status = err.status ?? err.response?.status
		console.error("STATUS:", status)
		console.error("TYPE:", err.error?.type || err.response?.data?.error?.type)
		console.error("MESSAGE:", err.message || err.response?.data?.error?.message)
	}
}

(async () => {
	await main()
})()