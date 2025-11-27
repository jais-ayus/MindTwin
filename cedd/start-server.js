const { spawn } = require('child_process');
const http = require('http');
const open = require('open');

const CONFIG = {
    backendPort: 3000,
    webServerPort: 8081,
    backendPath: './backend/server.js',
    dashboardUrl: 'http://localhost:8081/dashboard.html',
    webglUrl: 'http://localhost:8081/index.html',
    autoOpenBrowser: true,
    openDashboard: true,
    backendStartupTimeout: 10000, // 10 seconds
    healthCheckRetries: 10,
    healthCheckInterval: 1000 // 1 second
};

let backendProcess = null;
let webServerProcess = null;

// Check if Node.js is available
function checkNodeVersion() {
    const nodeVersion = process.version;
    console.log(`[Startup] Node.js version: ${nodeVersion}`);
    const majorVersion = parseInt(nodeVersion.split('.')[0].substring(1));
    if (majorVersion < 14) {
        console.error('[Startup] Error: Node.js version 14 or higher is required');
        process.exit(1);
    }
}

// Wait for backend to be ready
function waitForBackend(port, retries, interval) {
    return new Promise((resolve, reject) => {
        let attempts = 0;
        
        const checkHealth = () => {
            attempts++;
            const req = http.get(`http://localhost:${port}/api/health`, (res) => {
                if (res.statusCode === 200) {
                    console.log(`[Startup] Backend API is ready!`);
                    resolve();
                } else {
                    if (attempts < retries) {
                        setTimeout(checkHealth, interval);
                    } else {
                        reject(new Error(`Backend health check failed after ${retries} attempts`));
                    }
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
        
        // Start checking after a short delay
        setTimeout(checkHealth, 500);
    });
}

// Start backend API server
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
        
        // Wait for backend to be ready
        waitForBackend(CONFIG.backendPort, CONFIG.healthCheckRetries, CONFIG.healthCheckInterval)
            .then(() => resolve())
            .catch((error) => {
                console.error('[Startup] Backend startup failed:', error.message);
                if (backendProcess) {
                    backendProcess.kill();
                }
                reject(error);
            });
    });
}

// Start web server
function startWebServer() {
    return new Promise((resolve, reject) => {
        console.log('[Startup] Starting web server...');
        
        // Use http-server package via npx
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
        
        // Give web server a moment to start
        setTimeout(() => {
            console.log(`[Startup] Web server started on port ${CONFIG.webServerPort}`);
            console.log(`[Startup] Dashboard: ${CONFIG.dashboardUrl}`);
            console.log(`[Startup] WebGL Build: ${CONFIG.webglUrl}`);
            resolve();
        }, 2000);
    });
}

// Open browser
function openBrowser() {
    if (CONFIG.autoOpenBrowser) {
        const url = CONFIG.openDashboard ? CONFIG.dashboardUrl : CONFIG.webglUrl;
        console.log(`[Startup] Opening browser: ${url}`);
        open(url).catch(err => {
            console.warn('[Startup] Could not open browser automatically:', err.message);
            console.log(`[Startup] Please open manually: ${url}`);
        });
    }
}

// Cleanup function
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
    
    setTimeout(() => {
        process.exit(0);
    }, 1000);
}

// Handle process termination
process.on('SIGINT', cleanup);
process.on('SIGTERM', cleanup);
process.on('uncaughtException', (error) => {
    console.error('[Startup] Uncaught exception:', error);
    cleanup();
});

// Main startup function
async function start() {
    try {
        console.log('========================================');
        console.log('  IoT WebGL Dashboard Startup');
        console.log('========================================\n');
        
        checkNodeVersion();
        
        // Start backend
        await startBackend();
        
        // Start web server
        await startWebServer();
        
        // Open browser
        openBrowser();
        
        console.log('\n[Startup] All servers are running!');
        console.log('[Startup] Press Ctrl+C to stop\n');
        
    } catch (error) {
        console.error('\n[Startup] Startup failed:', error.message);
        cleanup();
        process.exit(1);
    }
}

// Start the application
start();

