# IoT WebGL Dashboard - Single Command Launch

This system provides a unified IoT monitoring dashboard that connects your Unity WebGL build with a web-based dashboard, all launched with a single command.

## Architecture

- **Backend API** (Port 3000): Receives data from WebGL build and serves it to the dashboard
- **Web Server** (Port 8081): Serves both the WebGL build and the IoT Dashboard
- **WebGL Build**: Unity WebGL application that sends component data to the API
- **IoT Dashboard**: Web-based dashboard that displays real-time component data

## Prerequisites

- **Node.js** (v14 or higher) - [Download](https://nodejs.org/)
- **npm** (comes with Node.js)
- Unity WebGL build in this folder

## Quick Start

### First Time Setup

1. **Install dependencies:**
   ```bash
   cd cedd
   npm install
   cd backend
   npm install
   cd ..
   ```

2. **Configure Unity Scene:**
   - Open your Unity scene (DemoRealVirtual)
   - Add `WebGLIoTMonitor` component to a GameObject
   - Configure API URL if needed (default: `http://localhost:3000`)
   - Build for WebGL to this folder

### Running the System

**Single Command:**
```bash
node start-server.js
```

This will:
1. Start the backend API server on port 3000
2. Start the web server on port 8081
3. Open the dashboard in your browser automatically

**Access URLs:**
- **Dashboard**: http://localhost:8081/dashboard.html
- **WebGL Build**: http://localhost:8081/index.html

**Stop Server:**
Press `Ctrl+C` in the terminal

## Unity Setup

### Adding WebGL IoT Monitor to Your Scene

1. **Open your scene** (e.g., DemoRealVirtual)

2. **Create or select a GameObject** to hold the monitor

3. **Add Components:**
   - Add `WebGLIoTMonitor` component
   - `WebGLDataSender` will be added automatically

4. **Configure Settings:**
   - **Api Base Url**: `http://localhost:3000` (default)
   - **Update Interval**: `0.1` seconds (how often to update component data)
   - **Send Interval**: `0.5` seconds (how often to send to API)
   - Enable/disable component types to monitor

5. **Build for WebGL:**
   - File → Build Settings → WebGL
   - Build to this folder (`cedd/`)
   - Make sure `index.html` is in the root of this folder

## File Structure

```
cedd/
├── index.html              # Unity WebGL entry point
├── Build/                  # Unity WebGL build files
├── TemplateData/           # Unity template files
├── dashboard.html          # IoT Dashboard
├── dashboard.css           # Dashboard styles
├── dashboard.js            # Dashboard logic
├── start-server.js         # Main startup script
├── package.json            # Node.js dependencies
├── backend/                # Backend API
│   ├── server.js
│   └── package.json
└── README.md
```

## Troubleshooting

### Backend API Not Starting

- Check if port 3000 is available: `netstat -ano | findstr :3000` (Windows)
- Make sure Node.js is installed: `node --version`
- Install backend dependencies: `cd backend && npm install`

### Web Server Not Starting

- Check if port 8081 is available
- Install http-server globally: `npm install -g http-server`
- Or use npx (already included in script)

### Dashboard Shows "Connection Error"

- Make sure backend API is running (check terminal)
- Verify API URL in dashboard.js: `http://localhost:3000`
- Check browser console for CORS errors
- Make sure WebGL build is running and sending data

### WebGL Build Not Sending Data

- Check Unity Console for errors
- Verify `WebGLIoTMonitor` component is added to scene
- Check API URL in component settings
- Verify components are discovered (check Unity Console logs)

### Port Already in Use

- Change ports in `start-server.js` CONFIG object
- Or stop the process using the port:
  - Windows: `netstat -ano | findstr :PORT` then `taskkill /PID <PID> /F`
  - Mac/Linux: `lsof -ti:PORT | xargs kill`

## Configuration

### Changing Ports

Edit `start-server.js`:
```javascript
const CONFIG = {
    backendPort: 3000,      // Change backend port
    webServerPort: 8081,    // Change web server port
    // ...
};
```

### Changing API URL in Dashboard

Edit `dashboard.js`:
```javascript
const API_CONFIG = {
    baseUrl: 'http://localhost:3000',  // Change API URL
    // ...
};
```

### Changing API URL in Unity

In Unity Inspector, set `WebGLIoTMonitor` → `Api Base Url` to your backend URL.

## Development

### Backend API Endpoints

- `GET /api/health` - Health check
- `POST /api/components` - Bulk update components
- `GET /api/components` - Get all components
- `GET /api/components/:id` - Get single component
- `GET /api/summary` - Get summary statistics

### Testing

1. Start the system: `node start-server.js`
2. Open dashboard: http://localhost:8081/dashboard.html
3. Open WebGL build: http://localhost:8081/index.html
4. Verify data flows from WebGL → API → Dashboard

## Support

For issues:
1. Check this README
2. Check browser console (F12)
3. Check Unity Console
4. Check terminal output
5. Verify all prerequisites are installed

---

**Enjoy your unified IoT WebGL Dashboard!** 🚀


