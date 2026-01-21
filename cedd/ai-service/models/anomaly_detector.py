from __future__ import annotations

from datetime import datetime
from typing import List

import numpy as np
from sklearn.ensemble import IsolationForest

from config import settings
from schemas import AnomalyRequest, AnomalyResponse, AnomalyResult
from utils.feature_engineering import build_feature_vector


class AnomalyDetector:
    def __init__(self) -> None:
        self.model = IsolationForest(
            n_estimators=50,
            contamination=0.05,
            random_state=42,
        )
        self._baseline_fit()

    def _baseline_fit(self) -> None:
        # Fit with a trivial baseline so the model can score immediately.
        dummy = np.array([[0.0], [1.0], [-1.0]])
        self.model.fit(dummy)

    def detect(self, request: AnomalyRequest) -> AnomalyResponse:
        results: List[AnomalyResult] = []

        for component in request.components:
            features = build_feature_vector(component)
            score = float(-self.model.decision_function([[features.z_score]])[0])
            severity = self._severity(score)
            explanation = self._explain(component, features.z_score, score)

            results.append(
                AnomalyResult(
                    componentId=component.name,
                    score=round(score, 3),
                    severity=severity,
                    explanation=explanation,
                    recommendations=self._recommend(component, severity),
                )
            )

        return AnomalyResponse(
            success=True,
            anomalies=results,
            timestamp=datetime.utcnow().isoformat(),
        )

    def _severity(self, score: float) -> str:
        threshold = settings.anomaly_default_threshold
        if score >= threshold * 1.5:
            return "critical"
        if score >= threshold:
            return "high"
        if score >= threshold * 0.6:
            return "medium"
        return "low"

    def _explain(self, component, z_score: float, score: float) -> str:
        direction = "above" if z_score > 0 else "below"
        return (
            f"{component.name} deviates {abs(z_score):.2f}σ {direction} rolling mean "
            f"(anomaly score {score:.2f})."
        )

    def _recommend(self, component, severity: str) -> List[str]:
        if severity in {"critical", "high"}:
            return [f"Inspect {component.name} immediately", "Verify PLC mode and safety ranges"]
        if severity == "medium":
            return [f"Monitor {component.name} closely over the next cycle"]
        return ["No action required"]


