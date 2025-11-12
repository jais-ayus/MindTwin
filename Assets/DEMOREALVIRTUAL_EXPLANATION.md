# DemoRealVirtual Scene Explanation

## Overview

The **DemoRealVirtual** scene (`Assets/realvirtual/Scenes/DemoRealvirtual.unity`) is a comprehensive demonstration scene that showcases the realvirtual (formerly game4automation) framework capabilities. It demonstrates a complete automation system with conveyors, robots, gantries, and PLC control logic.

---

## Scene Structure

### 1. **Core Controller**
- **realvirtualController**: The main controller component that must be present in every realvirtual scene
  - Located in the scene hierarchy
  - Manages global settings, scale, and initialization
  - Handles scene loading and component initialization through interfaces (`IInitStart`, `IInitAwake`, `IInitEnable`, etc.)
  - Reference: `Assets/realvirtual/private/realvirtualController.cs`

### 2. **Main Components**

#### **Robot System**
- **Robot Prefab**: A complete robot system with:
  - Multiple axes for movement
  - Gripper component for picking/placing objects
  - Animator controller for robot movements
  - PLC control interface
  - Prefab reference: `Assets/realvirtual/Scenes/DataForDemos/Prefabs/` (Robot prefab)

#### **Conveyor Systems**
- **Can Conveyor**: Transports cans/products
- **Box Conveyor**: Transports boxes
- Both use `Drive` components for movement
- Sensors detect when products are present

#### **Gantry System**
- Gantry with Y and Z axes for 2D movement
- Gripper for picking and placing cans
- Controlled by PLC logic

---

## Control Logic (PLC Scripts)

The scene uses several PLC (Programmable Logic Controller) scripts that implement the automation logic:

### 1. **PLC_CanConveyor.cs**
**Purpose**: Controls the conveyor that transports cans

**Key Logic**:
- Starts conveyor when sensor is NOT occupied and system is ON
- Stops conveyor when sensor detects a can
- Controls indicator lamp based on sensor state

**Key Components**:
- `StartConveyor`: Output signal to start/stop conveyor
- `SensorOccupied`: Input from sensor detecting cans
- `ButtonConveyorOn`: Manual control button
- `LampCanAtPosition`: Output to indicator lamp

**Behavior**:
```csharp
if (SensorOccupied.Value == false && On && converyoron)
    StartConveyor.Value = true;  // Start moving
else
    StartConveyor.Value = false;  // Stop
```

---

### 2. **PLC_Handling.cs**
**Purpose**: Controls the gantry loader system that picks cans from the conveyor and places them in a grid pattern

**State Machine**:
The system uses a state machine with the following states:

1. **"waiting"**: Waiting for a can to arrive at the sensor
2. **"drivingtopick"**: Moving gantry to pick position
3. **"atpickposition"**: Arrived at pick position
4. **"closinggripper"**: Closing gripper to grab can
5. **"lifting"**: Lifting can up
6. **"drivingtoplace"**: Moving to placement position
7. **"lifttingdown"**: Lowering can to placement position
8. **"openinggripper"**: Releasing can
9. **"drivingupafterplace"**: Moving up after placement
10. **"drivingtowaitpos"**: Returning to waiting position

**Key Features**:
- **Grid Placement**: Places cans in a grid pattern (configurable rows and columns)
- **Position Calculation**: 
  ```csharp
  placepos = PlaceCanPosY + (CurrentColNumber - 1) * DistanceCol;
  ```
- **Row Management**: Automatically moves to next row when column limit reached
- **Cycle Management**: Coordinates with box conveyor for box changes

**Key Components**:
- Gantry Y and Z axes (destination and start signals)
- Gripper open/close controls
- Sensor for detecting cans
- Integration with `PLC_BoxConveyor` for coordination

---

### 3. **PLC_Robot.cs**
**Purpose**: Controls the robot that picks up boxes, dumps cans from them, and places boxes on conveyor

**State Machine**:
1. **"waiting"**: Waiting for start signal
2. **"DriveToPick"**: Moving robot to pick position (animation: "MoveToPick")
3. **"CloseGripper"**: Closing gripper to grab box
4. **"DroppingCans"**: Lifting and dumping cans from box (animation: "LiftAndDump")
5. **"PlaceBox"**: Placing box on conveyor (animation: "PutBoxOnConveyor")
6. **"OpeningGripper"**: Releasing box
7. **"MovingToWaitPos"**: Returning to waiting position (animation: "MoveOutFromPick")

**Key Features**:
- **Animation-Based**: Uses Unity Animator for smooth robot movements
- **Can Unloading**: Automatically unloads all MUs (Material Units) from boxes when dumping
- **Counter**: Tracks number of cans unloaded
- **Axis Monitoring**: Monitors robot axis 6 rotation for dumping detection

**Animation Detection**:
```csharp
private bool AnimationFinished(string name)
{
    return (RobotAnimator.GetCurrentAnimatorStateInfo(0).IsName(name) &&
            RobotAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f);
}
```

---

### 4. **PLC_BoxConveyor.cs**
**Purpose**: Controls the conveyor system that transports boxes

**Key Features**:
- Manages box movement on conveyor
- Coordinates with handling system
- Handles row changes for grid placement
- Manages box change cycles

**Integration**:
- Works with `PLC_Handling` to coordinate gantry movements
- Provides signals for when gantry area is occupied
- Controls conveyor forward/backward movement

---

### 5. **DemoRDKControl.cs**
**Purpose**: Alternative robot control script (RDK = Robot Development Kit)

**Functionality**:
- Controls entry and exit drives
- Manages robot gripper pick/place operations
- Uses step-based state machine
- Coordinates with sensors for product detection

**State Flow**:
1. Monitor entry sensor
2. When product detected, signal robot to move to pick position
3. Pick product with gripper
4. Move to place position
5. Place product
6. Wait for release
7. Resume entry conveyor

---

## System Workflow

### Complete Cycle:

1. **Can Generation** → Cans are generated and placed on the can conveyor
2. **Can Transport** → `PLC_CanConveyor` moves cans forward until sensor detects one
3. **Can Pickup** → `PLC_Handling` detects can, moves gantry to pick position
4. **Grid Placement** → Gantry places can in grid pattern (row by row, column by column)
5. **Box Filling** → When grid is full, boxes are created/filled
6. **Box Transport** → `PLC_BoxConveyor` moves boxes to robot area
7. **Robot Pickup** → `PLC_Robot` picks up box
8. **Can Unloading** → Robot dumps cans from box
9. **Box Placement** → Robot places empty box on exit conveyor
10. **Cycle Repeat** → Process repeats

---

## Key realvirtual Components Used

### **Drive**
- Controls conveyor belt movement
- Has `JogForward` property to start/stop
- Speed configurable

### **Sensor**
- Detects when Material Units (MUs) are present
- `Occupied` property indicates detection
- Used throughout for position detection

### **Grip**
- Robot/gantry gripper component
- `PickObjects` and `PlaceObjects` properties
- `PickedMUs` list tracks held objects

### **Source**
- Generates Material Units (MUs)
- Can be automatic or manual
- `GenerateIfDistance` for automatic generation

### **Sink**
- Removes MUs from the system
- End point for products

### **MU (Material Unit)**
- Represents products/objects in the system
- Can contain other MUs (nested)
- Has `UnloadAllMUs()` method

### **PLC Signals**
- `PLCInputBool`, `PLCOutputBool`: Boolean signals
- `PLCInputInt`, `PLCOutputInt`: Integer signals
- `PLCInputFloat`, `PLCOutputFloat`: Float signals
- Used for communication between PLC scripts

---

## Initialization Sequence

When the scene starts:

1. **realvirtualController.Awake()**:
   - Sets up global controller reference
   - Initializes settings

2. **realvirtualController.OnEnable()**:
   - Calls `IInitEnable` interfaces
   - Sets up scene management

3. **realvirtualController.Start()**:
   - Calls `IInitStart` interfaces
   - Loads additive scenes if configured
   - Initializes all realvirtual components

4. **PLC Scripts Start()**:
   - Each PLC script initializes its state
   - Sets initial values for outputs
   - Resets counters and status

5. **PLC Scripts FixedUpdate()**:
   - Main control logic runs every physics frame
   - State machines advance
   - Signals are updated

---

## Demo Scene Features

### **HMI Integration** (if Professional version)
- `StartDemoSceneHMI.cs`: Automatically starts demo when scene loads
- Controls camera views (if Cinemachine installed)
- Manages HMI tabs and switches

### **Camera System**
- Multiple camera positions for different views
- Camera position assets in `Assets/realvirtual/Scenes/DataForDemos/`
- Virtual camera controller for view switching

### **Animations**
- Robot animations in `Assets/realvirtual/Scenes/DataForDemos/Animations/`
- Animator controller: `RobotController.controller`
- Animations: MoveToPick, LiftAndDump, PutBoxOnConveyor, MoveOutFromPick

---

## How to Use the Demo

1. **Open Scene**: 
   - Menu: `realvirtual/Open demo scene`
   - Or directly: `Assets/realvirtual/Scenes/DemoRealvirtual.unity`

2. **Press Play**: The system will automatically start

3. **Observe**:
   - Cans moving on conveyor
   - Gantry picking and placing cans in grid
   - Robot picking boxes and dumping cans
   - Complete automation cycle

4. **Control** (if HMI available):
   - Use HMI switches to start/stop systems
   - Switch camera views
   - Monitor status indicators

---

## Code References

- **Main Controller**: `Assets/realvirtual/private/realvirtualController.cs`
- **PLC Scripts**: `Assets/realvirtual/Scenes/DataForDemos/Scripts/`
  - `PLC_CanConveyor.cs`
  - `PLC_Handling.cs`
  - `PLC_Robot.cs`
  - `PLC_BoxConveyor.cs`
  - `DemoRDKControl.cs`
- **HMI Script**: `Assets/realvirtual/Professional/HMI/Demo/StartDemoSceneHMI.cs`
- **Prefabs**: `Assets/realvirtual/Scenes/DataForDemos/Prefabs/`

---

## Key Design Patterns

1. **State Machine Pattern**: All PLC scripts use state machines for sequential control
2. **Signal-Based Communication**: PLC scripts communicate via input/output signals
3. **Component-Based Architecture**: Each physical component (conveyor, robot, sensor) is a separate component
4. **Interface-Based Initialization**: Components implement interfaces for initialization order control

---

## Summary

The DemoRealVirtual scene is a complete, working example of:
- ✅ Conveyor systems with sensors
- ✅ Robot control with animations
- ✅ Gantry systems with 2D movement
- ✅ PLC-based control logic
- ✅ State machine implementation
- ✅ Material Unit (MU) handling
- ✅ System coordination and integration
- ✅ HMI integration (if available)

It serves as both a demonstration and a learning resource for building automation systems with the realvirtual framework.

