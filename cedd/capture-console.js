const puppeteer = require('puppeteer');

async function run() {
    const browser = await puppeteer.launch({
        headless: true,
        args: [
            '--no-sandbox',
            '--disable-setuid-sandbox',
            '--disable-dev-shm-usage',
            '--use-gl=swiftshader',
            '--disable-gpu'
        ]
    });

    const page = await browser.newPage();

    page.on('console', (msg) => {
        try {
            console.log(`[PAGE ${msg.type().toUpperCase()}] ${msg.text()}`);
        } catch (err) {
            console.log('[PAGE LOG] (unserializable message)', err);
        }
    });

    page.on('pageerror', (err) => {
        console.error('[PAGE ERROR]', err);
    });

    page.on('requestfailed', (req) => {
        console.error(`[REQUEST FAILED] ${req.url()} - ${req.failure()?.errorText}`);
    });

    try {
        await page.goto('http://127.0.0.1:8081/index.html', {
            waitUntil: 'load',
            timeout: 60000
        });

        // Allow time for Unity to finish booting and surface errors
        await page.waitForTimeout(15000);
    } finally {
        await browser.close();
    }
}

run().catch((err) => {
    console.error('[RUN ERROR]', err);
    process.exit(1);
});

