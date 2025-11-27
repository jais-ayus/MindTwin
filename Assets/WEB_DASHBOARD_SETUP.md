# IoT Web Dashboard Setup Guide

## Overview

The IoT Web Dashboard is a **web-based monitoring system** that displays real-time data from all IoT components in the DemoRealVirtual scene. It runs a local web server that serves a beautiful, responsive dashboard accessible through any web browser.

## Features

✅ **Web-Based Dashboard** - Access from any device with a web browser  
✅ **Real-time Updates** - Auto-refreshes component data every second  
✅ **Beautiful UI** - Modern, responsive design with gradient backgrounds  
✅ **REST API** - JSON API for programmatic access  
✅ **Zero Scene Modification** - Works alongside existing scene without changes  
✅ **Cross-Platform** - Works on Windows, Mac, Linux, mobile devices  

## Quick Setup

### Method 1: Automatic Setup (Recommended)

1. **Open the DemoRealVirtual scene** (`Assets/realvirtual/Scenes/DemoRealvirtual.unity`)

2. **Create an empty GameObject**:
   - Right-click in Hierarchy → Create Empty
   - Name it: `IoT Web Server Setup`

3. **Add the Setup Component**:
   - Select the GameObject
   - In Inspector, click "Add Component"
   - Search for: `IoTWebServerSetup`
   - Add it

4. **Configure Settings** (optional):
   - **Server Port**: Default is 8080 (change if needed)
   - **Monitor Update Interval**: Default is 0.1s

5. **Press Play** ▶️
   - The web server will start automatically
   - Check Unity Console for the server URL

6. **Open Web Browser**:
   - Navigate to: `http://localhost:8080/dashboard.html`
   - The dashboard will appear with all IoT components!

### Method 2: Manual Setup

1. **Create IoT Monitor**:
   - Create Empty GameObject → Name: `IoT Monitor`
   - Add Component: `IoTMonitor`
   - Configure update interval

2. **Create Web Server**:
   - Create Empty GameObject → Name: `IoT Web Server`
   - Add Component: `IoTWebServer`
   - Drag `IoT Monitor` to the Monitor field
   - Set Server Port (default: 8080)

3. **Press Play** ▶️

4. **Open Browser**: Navigate to `http://localhost:8080/dashboard.html`

## Accessing the Dashboard

### Local Access
- **URL**: `http://localhost:8080/dashboard.html`
- Works on the same machine running Unity

### Network Access (Same Network)
To access from other devices on your network:

1. Find your computer's IP address:
   - **Windows**: Open Command Prompt, type `ipconfig`, look for IPv4 Address
   - **Mac/Linux**: Open Terminal, type `ifconfig` or `ip addr`

2. Access from other device:
   - **URL**: `http://YOUR_IP_ADDRESS:8080/dashboard.html`
   - Example: `http://192.168.1.100:8080/dashboard.html`

**Note**: Make sure firewall allows connections on port 8080

## Dashboard Features

### Header
- **Title**: "🔌 IoT Insight Dashboard"
- **Refresh Button**: Manually refresh data
- **Auto Refresh Toggle**: Enable/disable automatic updates

### Summary Bar
- Total component count
- Active component count
- Breakdown by component type

### Component Cards
Each card displays:
- **Status Indicator**: Color-coded dot (Green=Active, Red=Stopped, etc.)
- **Component Name**: Name and type
- **Status**: Current state (e.g., "Running", "Occupied", "At Target")
- **Value**: Current value with unit (e.g., "200.00 mm/s", "1.00 State")

## API Endpoints

The web server provides REST API endpoints:

### GET `/api/components`
Returns JSON array of all components:
```json
[
  {
    "name": "Sensor",
    "type": "Sensor",
    "active": true,
    "status": "Occupied",
    "value": 1.0,
    "unit": "State",
    "color": "#00FF00"
  },
  ...
]
```

### GET `/api/summary`
Returns summary statistics:
```json
{
  "total": 26,
  "active": 24,
  "counts": {
    "Sensor": 6,
    "Drive": 5,
    "Axis": 3,
    ...
  }
}
```

### GET `/dashboard.html`
Serves the main dashboard HTML page

## Component Status Colors

| Color | Meaning |
|-------|---------|
| 🟢 Green | Active/Running/Occupied |
| 🔴 Red | Stopped/Inactive |
| 🟡 Yellow | Moving/In Progress |
| 🔵 Blue | Open/Ready |
| ⚪ Gray | Inactive/Free |
| 🔵 Cyan | Flashing/Special State |

## Monitored Component Types

### Sensors
- **Status**: "Occupied" or "Free"
- **Value**: 1 (occupied) or 0 (free)
- **Unit**: State

### Drives
- **Status**: "Running" or "Stopped"
- **Value**: Current speed in mm/s
- **Unit**: mm/s

### Axes
- **Status**: "At Target" or "Moving"
- **Value**: Current position
- **Unit**: mm

### Grips
- **Status**: "Holding X" or "Open"
- **Value**: Number of objects held
- **Unit**: Objects

### Lamps
- **Status**: "ON", "OFF", or "ON (Flashing)"
- **Value**: 1 (on) or 0 (off)
- **Unit**: State

### Sources
- **Status**: "Active" or "Inactive"
- **Value**: 1 (active) or 0 (inactive)
- **Unit**: State

### Sinks
- **Status**: "Active"
- **Value**: 1
- **Unit**: State

## Configuration Options

### IoTWebServer Settings
- **Server Port**: Port number for web server (default: 8080)
- **Enable CORS**: Allow cross-origin requests (default: true)
- **Auto Start On Start**: Automatically start server when scene starts

### IoTMonitor Settings
- **Update Interval**: How often to update component data (default: 0.1s)
- **Auto Discover On Start**: Automatically find components when scene starts
- **Component Type Filters**: Enable/disable monitoring for each type

## Troubleshooting

### Dashboard Not Loading
- **Check**: Is Unity in Play mode? (Server only runs in Play mode)
- **Check**: Check Unity Console for server start message
- **Check**: Try `http://127.0.0.1:8080/dashboard.html` instead
- **Solution**: Make sure port 8080 is not used by another application

### "Connection Error" Message
- **Check**: Is the web server component added and enabled?
- **Check**: Is Unity in Play mode?
- **Check**: Check Unity Console for errors
- **Solution**: Restart Unity and try again

### Port Already in Use
- **Error**: "Failed to start server" or port binding error
- **Solution**: Change Server Port to another number (e.g., 8081, 9000)
- **Solution**: Close other applications using port 8080

### No Components Found
- **Check**: Are you in Play mode? (Components are discovered at runtime)
- **Check**: Does the scene have IoT components?
- **Solution**: Wait a moment after Play - discovery happens automatically

### Can't Access from Other Devices
- **Check**: Are devices on the same network?
- **Check**: Is firewall blocking port 8080?
- **Solution**: Add firewall exception for Unity/port 8080
- **Solution**: Use your computer's IP address instead of localhost

## Security Notes

⚠️ **Important**: This web server is for **local development only**!

- The server runs on localhost by default
- CORS is enabled for easy access
- No authentication is implemented
- **Do not expose this to the internet** without proper security measures

For production use, consider:
- Adding authentication
- Using HTTPS
- Restricting CORS
- Adding rate limiting
- Using a proper web server (nginx, Apache)

## Advanced Usage

### Custom Dashboard
You can create your own dashboard by:
1. Accessing the API endpoints directly
2. Building custom HTML/CSS/JavaScript
3. Using frameworks like React, Vue, or Angular

### Integration with Other Tools
The REST API can be integrated with:
- **Grafana**: For advanced visualization
- **Node-RED**: For IoT workflows
- **Custom Applications**: Any app that can make HTTP requests

### Data Export
You can save component data by:
1. Making API calls to `/api/components`
2. Saving the JSON response
3. Processing with Python, Excel, etc.

## Files Created

1. `Assets/IoTComponentData.cs` - Data structure
2. `Assets/IoTMonitor.cs` - Component monitoring
3. `Assets/IoTWebServer.cs` - Web server and dashboard
4. `Assets/IoTWebServerSetup.cs` - Setup helper

## Technical Details

### Server Technology
- Uses `HttpListener` (built into .NET)
- Runs in background thread
- Serves static HTML/CSS/JS and JSON API
- Supports CORS for cross-origin requests

### Update Mechanism
- Monitor updates components at configurable interval
- Web dashboard polls API every second (configurable)
- Real-time updates without page refresh

### Performance
- Lightweight HTTP server
- Efficient JSON serialization
- Minimal Unity overhead
- Suitable for local network use

## Example Use Cases

1. **Remote Monitoring**: View factory status from office
2. **Mobile Access**: Check system status from phone/tablet
3. **Multiple Viewers**: Multiple people can view simultaneously
4. **Integration**: Connect to other systems via REST API
5. **Data Logging**: Save API responses for analysis

## Support

For issues:
1. Check this guide first
2. Check Unity Console for errors
3. Verify all components are added correctly
4. Ensure you're in Play mode
5. Try restarting Unity

---

**Enjoy your web-based IoT dashboard!** 🌐🚀









