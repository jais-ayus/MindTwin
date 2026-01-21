from __future__ import annotations

from datetime import datetime
from typing import List

import numpy as np

from config import settings
from schemas import (
    ComponentTelemetry,
    MaintenancePrediction,
    MaintenanceRequest,
    MaintenanceResponse,
)
from utils.feature_engineering import rolling_trend


class PredictiveMaintenanceModel:
    def predict(self, request: MaintenanceRequest) -> MaintenanceResponse:
        predictions: List[MaintenancePrediction] = []
        lookahead = request.lookaheadHours or settings.maintenance_default_hours

        for component in request.components:
            prediction = self._predict_component(component, lookahead)
            predictions.append(prediction)

        return MaintenanceResponse(
            success=True,
            predictions=predictions,
            timestamp=datetime.utcnow().isoformat(),
        )

    def _predict_component(
        self, component: ComponentTelemetry, lookahead: int
    ) -> MaintenancePrediction:
        trend = rolling_trend(component)
        degradation_rate = abs(trend.rate)

        if degradation_rate == 0:
            time_to_failure = float(lookahead)
        else:
            time_to_failure = min(float(lookahead), 10.0 / degradation_rate)

        probability = float(min(0.95, degradation_rate * 2))
        maintenance_window = max(4.0, time_to_failure * 0.2)

        if trend.rate > 0:
            action = f"Inspect {component.name} for overheating or over-speed."
        else:
            action = f"Check {component.name} for stalling or under-performance."

        return MaintenancePrediction(
            componentId=component.name,
            timeToFailureHours=round(time_to_failure, 2),
            probability=round(probability, 2),
            maintenanceWindowHours=round(maintenance_window, 2),
            recommendedAction=action,
            confidence=round(1 - np.exp(-degradation_rate + 1e-3), 2),
        )


