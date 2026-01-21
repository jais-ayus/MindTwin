# MINDTWIN AI Integration Guide

This document describes the AI-ready endpoints and data formats for integrating machine learning models with the MINDTWIN IoT Dashboard.

## Overview

The MINDTWIN platform provides several endpoints designed for AI/ML integration:

1. **Telemetry Collection** - Historical time-series data for training
2. **AI Command Interface** - Send batch commands from AI models
3. **Prediction Hooks** - Integration points for anomaly detection and predictions
4. **Model Management** - Future model deployment and management

## Base URL

```
http://localhost:3000/api
```

---

## Telemetry Endpoints

### GET /api/telemetry

Retrieve historical telemetry data for AI/ML training and inference.

**Query Parameters:**
- `component` (optional) - Filter by component name
- `limit` (optional) - Limit number of data points returned
- `since` (optional) - ISO timestamp to filter data after this time

**Response:**
```json
{
  "success": true,
  "telemetry": {
    "ConveyorBelt1": [
      {
        "timestamp": "2024-01-15T10:30:00.000Z",
        "value": 150.5,
        "status": "Running",
        "active": true
      }
    ]
  },
  "totalComponents": 15,
  "timestamp": "2024-01-15T10:35:00.000Z"
}
```

### GET /api/telemetry/stats

Get statistical summaries of telemetry data.

**Response:**
```json
{
  "success": true,
  "stats": {
    "ConveyorBelt1": {
      "dataPoints": 500,
      "firstTimestamp": "2024-01-15T08:00:00.000Z",
      "lastTimestamp": "2024-01-15T10:35:00.000Z",
      "activePercentage": "95.20",
      "valueStats": {
        "min": 0,
        "max": 200,
        "avg": "145.50",
        "latest": 150
      }
    }
  },
  "totalComponents": 15,
  "maxPointsPerComponent": 1000
}
```

---

## AI Command Interface

### POST /api/ai/command

Send batch commands from AI models to control components.

**Request Body:**
```json
{
  "commands": [
    {
      "componentId": "ConveyorBelt1",
      "parameter": "TargetSpeed",
      "value": 175
    },
    {
      "componentId": "Source1",
      "parameter": "Enabled",
      "value": true
    }
  ],
  "priority": "normal",
  "aiModelId": "optimization_agent_v1",
  "confidence": 0.95
}
```

**Response:**
```json
{
  "success": true,
  "accepted": 2,
  "rejected": 0,
  "commands": [
    { "index": 0, "commandId": "ai_1705312500000_0_abc123" },
    { "index": 1, "commandId": "ai_1705312500000_1_def456" }
  ],
  "timestamp": "2024-01-15T10:35:00.000Z"
}
```

### GET /api/ai/command/status

Check status of AI-generated commands.

**Query Parameters:**
- `commandId` (optional) - Get specific command status
- `modelId` (optional) - Filter by AI model ID

**Response:**
```json
{
  "success": true,
  "commands": [
    {
      "commandId": "ai_1705312500000_0_abc123",
      "status": "completed",
      "componentId": "ConveyorBelt1",
      "parameter": "TargetSpeed",
      "timestamp": "2024-01-15T10:35:00.000Z",
      "aiMetadata": {
        "modelId": "optimization_agent_v1",
        "confidence": 0.95,
        "priority": "normal"
      }
    }
  ],
  "total": 1
}
```

---

## Prediction Endpoints

### POST /api/ai/predict

Submit data for AI prediction/anomaly detection.

**Request Body:**
```json
{
  "componentId": "ConveyorBelt1",
  "type": "anomaly_detection",
  "data": [
    { "timestamp": "2024-01-15T10:30:00.000Z", "value": 150 },
    { "timestamp": "2024-01-15T10:31:00.000Z", "value": 152 },
    { "timestamp": "2024-01-15T10:32:00.000Z", "value": 148 }
  ]
}
```

**Response:**
```json
{
  "success": true,
  "prediction": {
    "componentId": "ConveyorBelt1",
    "type": "anomaly_detection",
    "timestamp": "2024-01-15T10:35:00.000Z",
    "results": {
      "anomalyScore": 0.15,
      "confidence": 0.92,
      "dataPointsAnalyzed": 3,
      "predictions": [
        { "type": "trend", "value": "stable", "confidence": 0.9 }
      ],
      "recommendations": [
        "System operating normally",
        "Continue monitoring"
      ]
    },
    "modelInfo": {
      "modelId": "placeholder_v1",
      "version": "1.0.0"
    }
  }
}
```

### GET /api/ai/models

List available AI models and their status.

**Response:**
```json
{
  "success": true,
  "models": [
    {
      "id": "anomaly_detector_v1",
      "name": "Anomaly Detection Model",
      "type": "anomaly_detection",
      "status": "placeholder",
      "ready": false
    }
  ]
}
```

### GET /api/ai/status

Get overall AI system status.

**Response:**
```json
{
  "success": true,
  "status": {
    "telemetryActive": true,
    "telemetryComponents": 15,
    "telemetryMaxPoints": 1000,
    "aiCommandsInQueue": 2,
    "modelsReady": 0,
    "integrationStatus": "ready_for_integration"
  },
  "endpoints": {
    "telemetry": "/api/telemetry",
    "aiCommand": "/api/ai/command",
    "aiPredict": "/api/ai/predict"
  }
}
```

---

## Data Formats

### Component Types
- `Drive` - Conveyor belts, motors
- `Sensor` - Detection sensors
- `Lamp` - Indicator lights
- `Source` - Material generators
- `Grip` - Gripper/robot hands
- `Axis` - Linear/rotary axes
- `Sink` - Material consumers

### Parameter Types by Component

**Drive:**
- `TargetSpeed` (float, 0-500 mm/s)
- `JogForward` (boolean)
- `JogBackward` (boolean)
- `Enabled` (boolean)

**Axis:**
- `TargetPosition` (float, -1000 to 1000 mm)
- `TargetStartMove` (boolean)
- `Enabled` (boolean)

**Source:**
- `Enabled` (boolean)

**Grip:**
- `PickObjects` (boolean)
- `PlaceObjects` (boolean)
- `Enabled` (boolean)

**Lamp:**
- `LampOn` (boolean)
- `Flashing` (boolean)
- `Enabled` (boolean)

**Sensor:**
- `LimitSensorToTag` (string)
- `Enabled` (boolean)

---

## Integration Examples

### Python - Fetch Telemetry for Training

```python
import requests
import pandas as pd

# Fetch telemetry data
response = requests.get('http://localhost:3000/api/telemetry', params={
    'limit': 1000
})
data = response.json()

# Convert to DataFrame
for component, readings in data['telemetry'].items():
    df = pd.DataFrame(readings)
    df['component'] = component
    # Use for training...
```

### Python - Send AI Commands

```python
import requests

commands = [
    {'componentId': 'ConveyorBelt1', 'parameter': 'TargetSpeed', 'value': 175},
    {'componentId': 'ConveyorBelt2', 'parameter': 'TargetSpeed', 'value': 150}
]

response = requests.post('http://localhost:3000/api/ai/command', json={
    'commands': commands,
    'aiModelId': 'my_optimization_model',
    'confidence': 0.95
})

result = response.json()
print(f"Accepted: {result['accepted']}, Rejected: {result['rejected']}")
```

### Python - Anomaly Detection Integration

```python
import requests

# Get recent telemetry
telemetry = requests.get('http://localhost:3000/api/telemetry', params={
    'component': 'ConveyorBelt1',
    'limit': 100
}).json()

# Send to prediction endpoint
prediction = requests.post('http://localhost:3000/api/ai/predict', json={
    'componentId': 'ConveyorBelt1',
    'type': 'anomaly_detection',
    'data': telemetry['telemetry']['ConveyorBelt1']
}).json()

if prediction['prediction']['results']['anomalyScore'] > 0.7:
    print("Anomaly detected!")
```

---

## WebSocket Support (Future)

The system is prepared for WebSocket integration for real-time AI streaming:

```javascript
// Future implementation
const socket = io('http://localhost:3000');

socket.on('telemetry', (data) => {
    // Real-time telemetry stream
});

socket.on('prediction', (data) => {
    // Real-time prediction results
});
```

---

## Rate Limits

- Telemetry: No rate limit (internal use)
- AI Commands: 100 commands per batch
- Predictions: 10 requests per second

---

## Error Handling

All endpoints return errors in this format:

```json
{
  "success": false,
  "error": "Error description",
  "code": "ERROR_CODE",
  "details": {}
}
```

Common error codes:
- `COMPONENT_NOT_FOUND` - Invalid component ID
- `VALIDATION_ERROR` - Invalid request parameters
- `STATE_ERROR` - Operation not allowed in current state (e.g., production halted)
- `SERVER_ERROR` - Internal server error

---

## Security Considerations

For production deployment:
1. Add authentication (JWT/API keys) to AI endpoints
2. Implement rate limiting
3. Add audit logging for AI commands
4. Validate AI model signatures
5. Implement command approval workflows for critical operations

---

## Contact

For AI integration support, refer to the main MINDTWIN documentation or contact the development team.


