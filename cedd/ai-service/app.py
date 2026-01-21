from __future__ import annotations

from datetime import datetime
from typing import Dict, List

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

from config import settings
from models.anomaly_detector import AnomalyDetector
from models.offline_monitor import OfflineMonitor
from models.optimizer import ProcessOptimizer
from models.parameter_forecaster import ParameterForecaster
from models.predictive_maintenance import PredictiveMaintenanceModel
from schemas import (
    AlertPayload,
    AnomalyRequest,
    MaintenanceRequest,
    OfflineEvaluationRequest,
    OptimizationRequest,
    ParameterEvaluationRequest,
)
from utils.data_client import backend_client

app = FastAPI(
    title=settings.app_name,
    version=settings.api_version,
    docs_url="/docs",
    redoc_url="/redoc",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

anomaly_detector = AnomalyDetector()
maintenance_model = PredictiveMaintenanceModel()
optimizer = ProcessOptimizer()
offline_monitor = OfflineMonitor()
parameter_forecaster = ParameterForecaster()
recent_alerts: List[Dict] = []


@app.get("/health")
def health() -> Dict[str, object]:
    return {
        "status": "healthy",
        "timestamp": datetime.utcnow().isoformat(),
        "models": {
            "anomaly": True,
            "maintenance": True,
            "optimization": True
        },
    }


@app.get("/ai/models/status")
def model_status() -> Dict:
    return {
        "success": True,
        "models": [
            {"id": "anomaly_detector_v1", "status": "ready"},
            {"id": "predictive_maintenance_v1", "status": "ready"},
            {"id": "process_optimizer_v1", "status": "ready"},
        ],
        "timestamp": datetime.utcnow().isoformat(),
    }


@app.post("/ai/anomaly/detect")
def detect_anomalies(request: AnomalyRequest) -> Dict:
    components = request.components or backend_client.fetch_recent_components()
    if not components:
        raise HTTPException(status_code=400, detail="No telemetry available for anomaly detection")
    response = anomaly_detector.detect(AnomalyRequest(components=components))
    return response.model_dump()


@app.post("/ai/maintenance/predict")
def predict_maintenance(request: MaintenanceRequest) -> Dict:
    components = request.components or backend_client.fetch_recent_components()
    if not components:
        raise HTTPException(status_code=400, detail="No telemetry available for maintenance prediction")
    response = maintenance_model.predict(MaintenanceRequest(components=components, lookaheadHours=request.lookaheadHours))
    return response.model_dump()


@app.post("/ai/optimize")
def optimise(request: OptimizationRequest) -> Dict:
    components = request.components or backend_client.fetch_recent_components()
    if not components:
        raise HTTPException(status_code=400, detail="No telemetry available for optimisation")
    response = optimizer.optimise(OptimizationRequest(components=components, objective=request.objective, horizonMinutes=request.horizonMinutes))
    return response.model_dump()


@app.post("/ai/offline/evaluate")
def evaluate_offline(request: OfflineEvaluationRequest) -> Dict:
    if not request.components:
        raise HTTPException(status_code=400, detail="No components provided for offline evaluation")
    response = offline_monitor.evaluate(request)
    return response.model_dump()


@app.post("/ai/parameter/evaluate")
def evaluate_parameter(request: ParameterEvaluationRequest) -> Dict:
    if not request.evaluations:
        raise HTTPException(status_code=400, detail="No parameter evaluations provided")
    response = parameter_forecaster.evaluate(request)
    return response.model_dump()


@app.post("/ai/alerts/range")
def ingest_range_alert(alert: AlertPayload) -> Dict:
    recent_alerts.append(alert.model_dump())
    # Keep recent history bounded
    if len(recent_alerts) > 1000:
        recent_alerts.pop(0)
    return {"success": True, "stored": len(recent_alerts)}

