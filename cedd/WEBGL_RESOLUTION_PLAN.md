# WebGL Simulation Issue - Resolution Plan

## Problem Identified

**Symptom**: CNC machine processes MU but gates never open, robot never picks up MU in WebGL build (works fine in Unity Editor)

**Root Cause**: The `PLCDemoCNCLoadUnload.cs` script uses `async Task.Delay()` for timers, which **does not work reliably in WebGL builds**. WebGL has limited support for async/await and threading operations.

## Critical Code Sections

### Problem Area 1: Machine Timer (Lines 414-433)
```csharp
async void StartMachineTimer(float delay)
{
    machineTimerToken = new CancellationTokenSource();
    try
    {
        await Task.Delay(System.TimeSpan.FromSeconds(delay), machineTimerToken.Token);
        EndMachine(); // ← This never gets called in WebGL!
    }
    catch (System.OperationCanceledException) { }
}
```

**Issue**: `Task.Delay()` may not complete in WebGL, so `EndMachine()` never executes, preventing state transition from "Machining" to "WaitingForUnloading".

### Problem Area 2: Tooling Wheel Timer (Lines 435-454)
```csharp
async void StartToolingWheelTimer(float delay)
{
    toolingWheelToken = new CancellationTokenSource();
    try
    {
        await Task.Delay(System.TimeSpan.FromSeconds(delay), toolingWheelToken.Token);
        EndMoveToolingWheel(); // ← May not execute in WebGL
    }
    catch (System.OperationCanceledException) { }
}
```

**Issue**: Similar problem - timer may not complete in WebGL.

### State Machine Flow (The Broken Chain)

**Expected Flow:**
1. `MachineState = "StartMachine"` → Line 373
2. `StartMachineTimer(MachineCycleTime)` called → Line 378
3. Timer completes → `EndMachine()` called → Line 427
4. `MachineState = "WaitingForUnloading"` → Line 400
5. Gates open (`OpenDoor.Value = true`) → Line 390
6. Robot detects state and picks up MU → Line 305-320

**Actual Flow in WebGL:**
1. ✅ `MachineState = "StartMachine"`
2. ✅ `StartMachineTimer(MachineCycleTime)` called
3. ❌ Timer **never completes** (Task.Delay fails in WebGL)
4. ❌ `EndMachine()` **never called**
5. ❌ `MachineState` **stays "Machining"** forever
6. ❌ Gates **never open** (only open when state is "WaitingForUnloading")
7. ❌ Robot **never picks up** (waits for "WaitingForUnloading" state)

## Resolution Strategy

### Solution: Use Platform-Specific Compilation Directives

**Key Principle**: Use `#if UNITY_WEBGL` directives to have different code paths:
- **Unity Editor**: Keep existing async `Task.Delay()` code (works fine)
- **WebGL Build**: Use Unity Coroutines (works in WebGL)

**Why This Approach:**
- ✅ Editor version remains **completely unchanged**
- ✅ WebGL gets the fix it needs
- ✅ No risk of breaking Editor functionality
- ✅ Platform-specific optimization

### Implementation Plan

#### Step 1: Add Platform-Specific Field Declarations

**Current (Line 67-70):**
```csharp
private CancellationTokenSource machineTimerToken;
private CancellationTokenSource toolingWheelToken;
```

**New (Platform-Specific):**
```csharp
#if UNITY_WEBGL
    private Coroutine machineTimerCoroutine;
    private Coroutine toolingWheelCoroutine;
#else
    private CancellationTokenSource machineTimerToken;
    private CancellationTokenSource toolingWheelToken;
#endif
```

#### Step 2: Update Cancellation Logic (Platform-Specific)

**Current (Lines 145-152):**
```csharp
if (machineTimerToken != null)
{
    machineTimerToken.Cancel();
}
if (toolingWheelToken != null)
{
    toolingWheelToken.Cancel();
}
```

**New (Platform-Specific):**
```csharp
#if UNITY_WEBGL
    if (machineTimerCoroutine != null)
    {
        StopCoroutine(machineTimerCoroutine);
        machineTimerCoroutine = null;
    }
    if (toolingWheelCoroutine != null)
    {
        StopCoroutine(toolingWheelCoroutine);
        toolingWheelCoroutine = null;
    }
#else
    if (machineTimerToken != null)
    {
        machineTimerToken.Cancel();
    }
    if (toolingWheelToken != null)
    {
        toolingWheelToken.Cancel();
    }
#endif
```

#### Step 3: Replace StartMachineTimer Method (Platform-Specific)

**Current (Lines 414-433) - Keep for Editor:**
```csharp
async void StartMachineTimer(float delay)
{
    // Cancel any existing timer
    if (machineTimerToken != null)
    {
        machineTimerToken.Cancel();
    }
    
    machineTimerToken = new CancellationTokenSource();
    
    try
    {
        await Task.Delay(System.TimeSpan.FromSeconds(delay), machineTimerToken.Token);
        EndMachine();
    }
    catch (System.OperationCanceledException)
    {
        // Timer was cancelled, do nothing
    }
}
```

**New (Platform-Specific Implementation):**
```csharp
#if UNITY_WEBGL
    void StartMachineTimer(float delay)
    {
        // Stop existing coroutine if running
        if (machineTimerCoroutine != null)
        {
            StopCoroutine(machineTimerCoroutine);
        }
        
        machineTimerCoroutine = StartCoroutine(MachineTimerCoroutine(delay));
    }

    IEnumerator MachineTimerCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndMachine();
        machineTimerCoroutine = null;
    }
#else
    async void StartMachineTimer(float delay)
    {
        // Cancel any existing timer
        if (machineTimerToken != null)
        {
            machineTimerToken.Cancel();
        }
        
        machineTimerToken = new CancellationTokenSource();
        
        try
        {
            await Task.Delay(System.TimeSpan.FromSeconds(delay), machineTimerToken.Token);
            EndMachine();
        }
        catch (System.OperationCanceledException)
        {
            // Timer was cancelled, do nothing
        }
    }
#endif
```

#### Step 4: Replace StartToolingWheelTimer Method (Platform-Specific)

**Current (Lines 435-454) - Keep for Editor:**
```csharp
async void StartToolingWheelTimer(float delay)
{
    // Cancel any existing timer
    if (toolingWheelToken != null)
    {
        toolingWheelToken.Cancel();
    }
    
    toolingWheelToken = new CancellationTokenSource();
    
    try
    {
        await Task.Delay(System.TimeSpan.FromSeconds(delay), toolingWheelToken.Token);
        EndMoveToolingWheel();
    }
    catch (System.OperationCanceledException)
    {
        // Timer was cancelled, do nothing
    }
}
```

**New (Platform-Specific Implementation):**
```csharp
#if UNITY_WEBGL
    void StartToolingWheelTimer(float delay)
    {
        // Stop existing coroutine if running
        if (toolingWheelCoroutine != null)
        {
            StopCoroutine(toolingWheelCoroutine);
        }
        
        toolingWheelCoroutine = StartCoroutine(ToolingWheelTimerCoroutine(delay));
    }

    IEnumerator ToolingWheelTimerCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndMoveToolingWheel();
        toolingWheelCoroutine = null;
    }
#else
    async void StartToolingWheelTimer(float delay)
    {
        // Cancel any existing timer
        if (toolingWheelToken != null)
        {
            toolingWheelToken.Cancel();
        }
        
        toolingWheelToken = new CancellationTokenSource();
        
        try
        {
            await Task.Delay(System.TimeSpan.FromSeconds(delay), toolingWheelToken.Token);
            EndMoveToolingWheel();
        }
        catch (System.OperationCanceledException)
        {
            // Timer was cancelled, do nothing
        }
    }
#endif
```

**Note**: The `BlinkLight` async method (lines 87-102) stays as-is for both platforms since it's not critical for the main cycle.

## Detailed Code Changes Required

### File: `Assets/realvirtual/Scenes/DataForDemos/Scripts/PLCDemoCNCLoadUnload.cs`

**Important**: All changes use `#if UNITY_WEBGL` directives to ensure Editor code remains unchanged.

#### Change 1: Update Field Declarations (Line 67-70)
**Replace:**
```csharp
private CancellationTokenSource machineTimerToken;
private CancellationTokenSource toolingWheelToken;
```

**With:**
```csharp
#if UNITY_WEBGL
    private Coroutine machineTimerCoroutine;
    private Coroutine toolingWheelCoroutine;
#else
    private CancellationTokenSource machineTimerToken;
    private CancellationTokenSource toolingWheelToken;
#endif
```

#### Change 2: Update OnSwitch Cancellation (Lines 145-152)
**Replace:**
```csharp
if (machineTimerToken != null)
{
    machineTimerToken.Cancel();
}
if (toolingWheelToken != null)
{
    toolingWheelToken.Cancel();
}
```

**With:**
```csharp
#if UNITY_WEBGL
    if (machineTimerCoroutine != null)
    {
        StopCoroutine(machineTimerCoroutine);
        machineTimerCoroutine = null;
    }
    if (toolingWheelCoroutine != null)
    {
        StopCoroutine(toolingWheelCoroutine);
        toolingWheelCoroutine = null;
    }
#else
    if (machineTimerToken != null)
    {
        machineTimerToken.Cancel();
    }
    if (toolingWheelToken != null)
    {
        toolingWheelToken.Cancel();
    }
#endif
```

#### Change 3: Replace StartMachineTimer Method (Lines 414-433)
**Replace entire method with:**
```csharp
#if UNITY_WEBGL
    void StartMachineTimer(float delay)
    {
        // Stop existing coroutine if running
        if (machineTimerCoroutine != null)
        {
            StopCoroutine(machineTimerCoroutine);
        }
        
        machineTimerCoroutine = StartCoroutine(MachineTimerCoroutine(delay));
    }

    IEnumerator MachineTimerCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndMachine();
        machineTimerCoroutine = null;
    }
#else
    async void StartMachineTimer(float delay)
    {
        // Cancel any existing timer
        if (machineTimerToken != null)
        {
            machineTimerToken.Cancel();
        }
        
        machineTimerToken = new CancellationTokenSource();
        
        try
        {
            await Task.Delay(System.TimeSpan.FromSeconds(delay), machineTimerToken.Token);
            EndMachine();
        }
        catch (System.OperationCanceledException)
        {
            // Timer was cancelled, do nothing
        }
    }
#endif
```

#### Change 4: Replace StartToolingWheelTimer Method (Lines 435-454)
**Replace entire method with:**
```csharp
#if UNITY_WEBGL
    void StartToolingWheelTimer(float delay)
    {
        // Stop existing coroutine if running
        if (toolingWheelCoroutine != null)
        {
            StopCoroutine(toolingWheelCoroutine);
        }
        
        toolingWheelCoroutine = StartCoroutine(ToolingWheelTimerCoroutine(delay));
    }

    IEnumerator ToolingWheelTimerCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndMoveToolingWheel();
        toolingWheelCoroutine = null;
    }
#else
    async void StartToolingWheelTimer(float delay)
    {
        // Cancel any existing timer
        if (toolingWheelToken != null)
        {
            toolingWheelToken.Cancel();
        }
        
        toolingWheelToken = new CancellationTokenSource();
        
        try
        {
            await Task.Delay(System.TimeSpan.FromSeconds(delay), toolingWheelToken.Token);
            EndMoveToolingWheel();
        }
        catch (System.OperationCanceledException)
        {
            // Timer was cancelled, do nothing
        }
    }
#endif
```

## Testing Plan

### Test 1: Verify Timer Completion
- **Action**: Run WebGL build, let CNC process MU
- **Expected**: After `MachineCycleTime` seconds, gates should open
- **Check**: `MachineState` should change to "WaitingForUnloading"

### Test 2: Verify State Transitions
- **Action**: Monitor `MachineState` in browser console or debug UI
- **Expected**: State should transition: "StartMachine" → "Machining" → "WaitingForUnloading"
- **Check**: No state should get stuck

### Test 3: Verify Gate Opening
- **Action**: Watch CNC gates after processing starts
- **Expected**: Gates should open when state becomes "WaitingForUnloading"
- **Check**: `OpenDoor.Value` should become `true`

### Test 4: Verify Robot Pickup
- **Action**: After gates open, robot should pick up MU
- **Expected**: Robot should detect "WaitingForUnloading" state and start unloading program
- **Check**: `RobotState` should change to "UnloadingMachine"

### Test 5: Verify Complete Cycle
- **Action**: Full cycle from conveyor 1 to conveyor 2
- **Expected**: Complete cycle should work: Conveyor1 → Robot → CNC → Robot → Conveyor2
- **Check**: MU should exit on conveyor 2

## Additional Considerations

### Why This Solution Works

1. **Unity Coroutines are WebGL-Compatible**
   - Use Unity's frame-based execution
   - No threading dependencies
   - Reliable across all platforms

2. **WaitForSeconds is Time-Based**
   - Uses `Time.time` internally
   - Not frame-dependent
   - Works consistently in WebGL

3. **Maintains Same Functionality**
   - Same timing behavior
   - Same cancellation capability
   - Same state transitions

### Potential Edge Cases

1. **Frame Rate Variations**: 
   - `WaitForSeconds` handles this automatically
   - Uses scaled time, so pauses don't affect it

2. **Multiple Timer Starts**:
   - Handled by stopping existing coroutine before starting new one
   - Prevents multiple timers running simultaneously

3. **Scene Restart**:
   - Coroutines automatically stop when object is destroyed
   - No cleanup needed

## Implementation Checklist

- [ ] Backup original `PLCDemoCNCLoadUnload.cs` file
- [ ] Add `#if UNITY_WEBGL` directive around field declarations
- [ ] Add platform-specific field declarations (Coroutine for WebGL, CancellationTokenSource for Editor)
- [ ] Update cancellation logic in `FixedUpdate()` with `#if UNITY_WEBGL` directive
- [ ] Replace `StartMachineTimer()` method with platform-specific version
- [ ] Add `MachineTimerCoroutine()` IEnumerator method (WebGL only)
- [ ] Replace `StartToolingWheelTimer()` method with platform-specific version
- [ ] Add `ToolingWheelTimerCoroutine()` IEnumerator method (WebGL only)
- [ ] **Test in Unity Editor** (should work exactly as before - no changes)
- [ ] Build WebGL and test complete cycle
- [ ] Verify state transitions work correctly in WebGL
- [ ] Verify gates open after processing in WebGL
- [ ] Verify robot picks up MU in WebGL
- [ ] Verify complete cycle works end-to-end in WebGL
- [ ] **Re-test in Unity Editor** (confirm no regressions)

## Expected Outcome

After implementing these changes:

✅ CNC machine timer will complete correctly in WebGL  
✅ `EndMachine()` will be called after `MachineCycleTime` seconds  
✅ `MachineState` will transition from "Machining" to "WaitingForUnloading"  
✅ Gates will open when state becomes "WaitingForUnloading"  
✅ Robot will detect state and pick up MU from CNC  
✅ Complete cycle will work: Conveyor1 → Robot → CNC → Robot → Conveyor2  

## Notes

- **✅ Editor Version Protected** - Uses `#if UNITY_WEBGL` directives, Editor code remains **completely unchanged**
- **✅ WebGL Gets Fix** - WebGL build uses Coroutines which work reliably
- **✅ Zero Risk** - Editor functionality cannot be affected by WebGL-specific code
- **✅ Platform Optimization** - Each platform uses the best approach for its environment
- **✅ No Performance Impact** - Coroutines are efficient, async timers work fine in Editor

---

**Status**: Ready for Implementation  
**Priority**: High (Blocks WebGL functionality)  
**Complexity**: Low (Simple replacement of async timers with coroutines)

