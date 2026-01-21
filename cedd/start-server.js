const { spawn } = require('child_process');
const http = require('http');
const open = require('open');

const CONFIG = {
    backendPort: 3000,
    webServerPort: 8081,
    backendPath: './backend/server.js',
    landingUrl: 'http://localhost:8081/landing.html',
    dashboardUrl: 'http://localhost:8081/dashboard.html',
    webglUrl: 'http://localhost:8081/index.html',
    autoOpenBrowser: true,
    openLanding: true,
    healthCheckRetries: 10,
    healthCheckInterval: 1000
};

let backendProcess = null;
let webServerProcess = null;

function checkNodeVersion() {
    const nodeVersion = process.version;
    console.log(`[Startup] Node.js version: ${nodeVersion}`);
    const majorVersion = parseInt(nodeVersion.split('.')[0].substring(1));
    if (majorVersion < 14) {
        console.error('[Startup] Error: Node.js version 14 or higher is required');
        process.exit(1);
    }
}

function waitForBackend(port, retries, interval) {
    return new Promise((resolve, reject) => {
        let attempts = 0;
        
        const checkHealth = () => {
            attempts++;
            const req = http.get(`http://localhost:${port}/api/health`, (res) => {
                if (res.statusCode === 200) {
                    console.log('[Startup] Backend API is ready!');
                    resolve();
                } else if (attempts < retries) {
                        setTimeout(checkHealth, interval);
                    } else {
                        reject(new Error(`Backend health check failed after ${retries} attempts`));
                }
            });
            
            req.on('error', (err) => {
                if (attempts < retries) {
                    setTimeout(checkHealth, interval);
                } else {
                    reject(new Error(`Backend health check failed: ${err.message}`));
                }
            });
            
            req.setTimeout(2000, () => {
                req.destroy();
                if (attempts < retries) {
                    setTimeout(checkHealth, interval);
                } else {
                    reject(new Error('Backend health check timeout'));
                }
            });
        };
        
        setTimeout(checkHealth, 500);
    });
}

function startBackend() {
    return new Promise((resolve, reject) => {
        console.log('[Startup] Starting backend API server...');
        
        backendProcess = spawn('node', [CONFIG.backendPath], {
            cwd: __dirname,
            stdio: 'inherit',
            shell: true
        });
        
        backendProcess.on('error', (error) => {
            console.error('[Startup] Failed to start backend:', error.message);
            reject(error);
        });
        
        backendProcess.on('exit', (code) => {
            if (code !== 0 && code !== null) {
                console.error(`[Startup] Backend process exited with code ${code}`);
            }
        });
        
        waitForBackend(CONFIG.backendPort, CONFIG.healthCheckRetries, CONFIG.healthCheckInterval)
            .then(resolve)
            .catch((error) => {
                console.error('[Startup] Backend startup failed:', error.message);
                if (backendProcess) backendProcess.kill();
                reject(error);
            });
    });
}

function startWebServer() {
    return new Promise((resolve, reject) => {
        console.log('[Startup] Starting web server...');
        
        webServerProcess = spawn('npx', ['--yes', 'http-server', '-p', CONFIG.webServerPort.toString(), '-c-1', '--cors'], {
            cwd: __dirname,
            stdio: 'inherit',
            shell: true
        });
        
        webServerProcess.on('error', (error) => {
            console.error('[Startup] Failed to start web server:', error.message);
            console.error('[Startup] Make sure http-server is installed: npm install -g http-server');
            reject(error);
        });
        
        setTimeout(() => {
            console.log(`[Startup] Web server started on port ${CONFIG.webServerPort}`);
            console.log(`[Startup] Dashboard: ${CONFIG.dashboardUrl}`);
            console.log(`[Startup] WebGL Build: ${CONFIG.webglUrl}`);
            resolve();
        }, 2000);
    });
}

function openBrowser() {
    if (!CONFIG.autoOpenBrowser) return;
        const url = CONFIG.openLanding ? CONFIG.landingUrl : CONFIG.dashboardUrl;
        console.log(`[Startup] Opening browser: ${url}`);
        console.log(`[Startup] Landing Page: ${CONFIG.landingUrl}`);
        console.log(`[Startup] Dashboard: ${CONFIG.dashboardUrl}`);
        console.log(`[Startup] WebGL Build: ${CONFIG.webglUrl}`);
        open(url).catch(err => {
            console.warn('[Startup] Could not open browser automatically:', err.message);
            console.log(`[Startup] Please open manually: ${url}`);
        });
}

function cleanup() {
    console.log('\n[Startup] Shutting down servers...');
    
    if (webServerProcess) {
        webServerProcess.kill();
        console.log('[Startup] Web server stopped');
    }
    
    if (backendProcess) {
        backendProcess.kill();
        console.log('[Startup] Backend API stopped');
    }
    
    setTimeout(() => process.exit(0), 1000);
}

process.on('SIGINT', cleanup);
process.on('SIGTERM', cleanup);
process.on('uncaughtException', (error) => {
    console.error('[Startup] Uncaught exception:', error);
    cleanup();
});

async function start() {
    try {
        console.log('========================================');
        console.log('  IoT WebGL Dashboard Startup');
        console.log('========================================\n');
        
        checkNodeVersion();
        await startBackend();
        await startWebServer();
        openBrowser();
        
        console.log('\n[Startup] All servers are running!');
        console.log('[Startup] Press Ctrl+C to stop');
    } catch (error) {
        console.error('[Startup] Startup failed:', error.message);
        cleanup();
    }
}

start();
