import { createWriteStream } from "node:fs"
import { pipeline } from "node:stream"
import { promisify } from "node:util"

const streamPipeline = promisify(pipeline)

const urls = [
	"abc",
	"def"
]

for (const i in urls) {
	const url = urls[i]
	const res = await fetch(url)

	if (!res.ok) {
		throw new Error(`HTTP error ${res.status}`)
	}

	await streamPipeline(res.body, createWriteStream(`image${i}.png`))
}




console.log("Downloads complete!")