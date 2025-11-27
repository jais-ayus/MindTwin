# IoT Insight Dashboard Setup Guide

## Overview

The IoT Insight Dashboard is a **non-intrusive monitoring system** that displays real-time data from all IoT components in the DemoRealVirtual scene **without modifying the scene itself**.

## Features

✅ **Automatic Component Discovery** - Finds all Sensors, Drives, Axes, Grips, Lamps, Sources, and Sinks  
✅ **Real-time Monitoring** - Updates component status, values, and states in real-time  
✅ **Visual Dashboard** - Clean, organized UI showing all components with color-coded status  
✅ **Component Filtering** - Show/hide inactive components  
✅ **Summary Statistics** - Total component count and breakdown by type  
✅ **Zero Scene Modification** - Works alongside existing scene without changes  

## Quick Setup

### Method 1: Automatic Setup (Recommended)

1. **Open the DemoRealVirtual scene** (`Assets/realvirtual/Scenes/DemoRealvirtual.unity`)

2. **Create an empty GameObject**:
   - Right-click in Hierarchy → Create Empty
   - Name it: `IoT Dashboard Setup`

3. **Add the Setup Component**:
   - Select the GameObject
   - In Inspector, click "Add Component"
   - Search for: `IoTDashboardSetup`
   - Add it

4. **Press Play** ▶️
   - The dashboard will automatically appear on screen
   - All IoT components will be discovered and displayed

### Method 2: Manual Setup

1. **Create IoT Monitor**:
   - Create Empty GameObject → Name: `IoT Monitor`
   - Add Component: `IoTMonitor`
   - Configure update interval (default: 0.1s)

2. **Create IoT Dashboard**:
   - Create Empty GameObject → Name: `IoT Dashboard`
   - Add Component: `IoTDashboard`
   - Drag `IoT Monitor` to the Monitor field

3. **Press Play** ▶️

## Dashboard Components

### IoTMonitor.cs
- **Purpose**: Discovers and monitors all IoT components
- **Features**:
  - Auto-discovers Sensors, Drives, Axes, Grips, Lamps, Sources, Sinks
  - Updates component status at configurable intervals
  - Provides events for component discovery and updates
  - Tracks component values over time (history)

### IoTDashboard.cs
- **Purpose**: UI manager for displaying component data
- **Features**:
  - Creates Unity Canvas with scrollable component list
  - Displays component cards with name, type, status, and value
  - Color-coded status indicators
  - Summary statistics footer
  - Refresh button and auto-refresh toggle

### IoTComponentData.cs
- **Purpose**: Data structure for component information
- **Stores**:
  - Component name, type, status
  - Current value and unit
  - Status color
  - Component references
  - Value history for trends

### IoTDashboardSetup.cs
- **Purpose**: Easy setup helper script
- **Features**:
  - One-click dashboard creation
  - Automatic component initialization
  - Context menu options for setup/removal

## Dashboard UI Elements

### Header
- **Title**: "IoT Insight Dashboard"
- Dark blue background

### Component Cards
Each card displays:
- **Status Indicator**: Color bar (Green=Active, Red=Stopped, Yellow=Moving, etc.)
- **Component Name**: Name and type (e.g., "Sensor (Sensor)")
- **Status**: Current state (e.g., "Occupied", "Running", "At Target")
- **Value**: Current value with unit (e.g., "1.00 State", "200.00 mm/s")

### Footer
- **Summary**: Total components, active count, breakdown by type
- **Refresh Button**: Manually refresh component list
- **Auto Refresh Toggle**: Enable/disable automatic updates

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
- **Value**: Speed in mm/s
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

### IoTMonitor Settings
- **Update Interval**: How often to update component data (default: 0.1s)
- **Auto Discover On Start**: Automatically find components when scene starts
- **Component Type Filters**: Enable/disable monitoring for each type

### IoTDashboard Settings
- **Show Inactive Components**: Display components that are inactive
- **Card Spacing**: Space between component cards
- **Colors**: Customize header, active, and inactive colors

## Usage Tips

1. **First Time Setup**:
   - Add `IoTDashboardSetup` to any GameObject
   - Press Play - dashboard appears automatically

2. **Manual Refresh**:
   - Click "Refresh" button in footer
   - Or disable "Auto" toggle and refresh manually

3. **Hide Inactive**:
   - Set `Show Inactive Components` to false in dashboard settings
   - Only active components will be displayed

4. **Performance**:
   - Increase `Update Interval` if dashboard causes lag
   - Default 0.1s is good for most cases

5. **Removing Dashboard**:
   - Right-click `IoTDashboardSetup` component
   - Select "Remove IoT Dashboard" from context menu
   - Or simply delete the GameObjects

## Troubleshooting

### Dashboard Not Appearing
- **Check**: Is `IoTDashboardSetup` component added?
- **Check**: Is `Auto Setup On Start` enabled?
- **Solution**: Manually call `SetupDashboard()` method

### No Components Found
- **Check**: Are you in Play mode? (Components are discovered at runtime)
- **Check**: Does the scene have IoT components?
- **Solution**: Wait a moment after Play - discovery happens automatically

### Components Not Updating
- **Check**: Is "Auto" toggle enabled in footer?
- **Check**: Is `Update Interval` set correctly?
- **Solution**: Click "Refresh" button manually

### UI Overlaps Other Elements
- **Solution**: The dashboard uses Screen Space Overlay - it appears on top
- **Future**: Can be modified to use World Space for 3D positioning

## Technical Details

### Component Discovery
- Uses `FindObjectsByType<T>()` to find all components
- Runs once at start (or on manual refresh)
- Stores references for efficient updates

### Update Mechanism
- Updates run in `Update()` loop at specified interval
- Each component type has custom update logic
- Values stored in history for potential trend analysis

### UI Creation
- Canvas created programmatically
- Uses Unity UI (uGUI) and TextMeshPro
- Responsive layout with scroll view

## Future Enhancements

Potential additions:
- 📊 **Charts/Graphs**: Visualize value history over time
- 🔍 **Search/Filter**: Filter components by name or type
- 📈 **Trends**: Show value trends and statistics
- 🎨 **Themes**: Customizable color schemes
- 📱 **Mobile Layout**: Optimized for different screen sizes
- 💾 **Data Export**: Export component data to CSV/JSON
- 🔔 **Alerts**: Notifications for component state changes

## Files Created

1. `Assets/IoTComponentData.cs` - Data structure
2. `Assets/IoTMonitor.cs` - Component monitoring
3. `Assets/IoTDashboard.cs` - UI dashboard
4. `Assets/IoTDashboardSetup.cs` - Setup helper

## Notes

- ✅ **Scene remains untouched** - No modifications to DemoRealVirtual scene
- ✅ **Non-intrusive** - Can be added/removed without affecting scene
- ✅ **Runtime only** - Dashboard appears only in Play mode
- ✅ **Performance optimized** - Updates at configurable intervals

## Support

For issues or questions:
1. Check this guide first
2. Verify all components are added correctly
3. Check Unity Console for errors
4. Ensure you're in Play mode when testing

---

**Enjoy monitoring your IoT components!** 🚀









