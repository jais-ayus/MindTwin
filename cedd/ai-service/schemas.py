from __future__ import annotations

from typing import Any, Dict, List, Optional
from pydantic import BaseModel, Field


class ComponentTelemetry(BaseModel):
    name: str
    type: Optional[str] = Field(default=None, alias="componentType")
    value: float
    status: Optional[str] = None
    metadata: Dict[str, Any] = Field(default_factory=dict)


class TelemetryEnvelope(BaseModel):
    components: List[ComponentTelemetry]
    timestamp: Optional[str] = None


class AnomalyRequest(BaseModel):
    components: List[ComponentTelemetry]


class AnomalyResult(BaseModel):
    componentId: str
    score: float
    severity: str
    explanation: str
    recommendations: List[str] = Field(default_factory=list)


class AnomalyResponse(BaseModel):
    success: bool
    anomalies: List[AnomalyResult]
    timestamp: str


class MaintenanceRequest(BaseModel):
    components: List[ComponentTelemetry]
    lookaheadHours: Optional[int] = None


class MaintenancePrediction(BaseModel):
    componentId: str
    timeToFailureHours: float
    probability: float
    maintenanceWindowHours: float
    recommendedAction: str
    confidence: float


class MaintenanceResponse(BaseModel):
    success: bool
    predictions: List[MaintenancePrediction]
    timestamp: str


class OptimizationRequest(BaseModel):
    components: List[ComponentTelemetry]
    objective: Optional[str] = "throughput"
    horizonMinutes: Optional[int] = None


class OptimizationSuggestion(BaseModel):
    componentId: str
    parameter: str
    current: float
    recommended: float
    constraintsRespected: bool
    expectedImpact: Dict[str, float]
    explanation: str


class OptimizationResponse(BaseModel):
    success: bool
    suggestions: List[OptimizationSuggestion]
    timestamp: str


class AlertPayload(BaseModel):
    componentId: str
    parameter: str
    value: float
    limit: float
    source: str
    timestamp: str


class OfflineComponentState(BaseModel):
    componentId: str
    type: Optional[str] = None
    status: Optional[str] = None
    gapMs: int
    metadata: Dict[str, Any] = Field(default_factory=dict)
    criticality: Optional[str] = None
    manualOffline: Optional[bool] = None
    lastHeartbeat: Optional[str] = None
    thresholdMs: Optional[int] = None
    heartbeatTimeoutMs: Optional[int] = None
    manualOverride: Optional[bool] = None


class OfflineEvaluationRequest(BaseModel):
    components: List[OfflineComponentState]


class OfflineAlert(BaseModel):
    componentId: str
    severity: str
    gapSeconds: float
    recommendation: str
    likelihood: float
    reason: str
    autoStopRecommended: bool = False
    metadata: Dict[str, Any] = Field(default_factory=dict)


class OfflineEvaluationResponse(BaseModel):
    success: bool
    alerts: List[OfflineAlert]
    timestamp: str


class ParameterEvaluation(BaseModel):
    componentId: str
    componentType: Optional[str] = None
    parameter: str
    proposedValue: float
    currentValue: Optional[float] = None
    defaultValue: Optional[float] = None
    minValue: Optional[float] = None
    maxValue: Optional[float] = None
    recommendedMin: Optional[float] = None
    recommendedMax: Optional[float] = None
    metadata: Dict[str, Any] = Field(default_factory=dict)
    context: Dict[str, Any] = Field(default_factory=dict)


class ParameterEvaluationRequest(BaseModel):
    evaluations: List[ParameterEvaluation]


class ParameterWarning(BaseModel):
    componentId: str
    parameter: str
    risk: str
    throughputImpact: float
    wearMultiplier: float
    estimatedRULHours: float
    notes: List[str] = Field(default_factory=list)
    suggestions: List[str] = Field(default_factory=list)
    value: float
    defaultValue: Optional[float] = None
    recommendedRange: Dict[str, Optional[float]] = Field(default_factory=dict)
    metadata: Dict[str, Any] = Field(default_factory=dict)


class ParameterEvaluationResponse(BaseModel):
    success: bool
    warnings: List[ParameterWarning]
    timestamp: str


