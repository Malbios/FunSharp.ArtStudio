// diag.js
const path = require('path');

try {
	const puppeteerExtra = require('puppeteer-extra');
	const puppeteer = require('puppeteer');        // the package that should be installed
	const puppeteerCore = require('puppeteer-core');

	console.log('require.resolve puppeteer-extra ->', require.resolve('puppeteer-extra'));
	console.log('require.resolve puppeteer ->', require.resolve('puppeteer'));
	console.log('puppeteer version ->', require('puppeteer/package.json').version);
	console.log('puppeteer-core version ->', require('puppeteer-core/package.json').version);
	console.log('puppeteer-extra version ->', require('puppeteer-extra/package.json').version);

	(async () => {
		const browser = await puppeteerExtra.launch({ headless: true });
		const page = await browser.newPage();

		console.log('page constructor name ->', page.constructor && page.constructor.name);
		console.log('Prototype methods (first 60) ->',
			Object.getOwnPropertyNames(Object.getPrototypeOf(page)).sort().slice(0, 60));

		// show whether $x exists and types of common functions
		console.log('typeof page.$x ->', typeof page.$x);
		console.log('typeof page.$ ->', typeof page.$);
		console.log('typeof page.$$eval ->', typeof page.$$eval);
		console.log('typeof page.evaluate ->', typeof page.evaluate);
		await browser.close();
	})().catch(e => {
		console.error('ERROR during Puppeteer run:', e);
	});
} catch (err) {
	console.error('ERROR loading modules:', err);
}
