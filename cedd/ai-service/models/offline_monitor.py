from __future__ import annotations

from datetime import datetime
from typing import List, Optional, Tuple

from schemas import (
    OfflineAlert,
    OfflineComponentState,
    OfflineEvaluationRequest,
    OfflineEvaluationResponse,
)


class OfflineMonitor:
    """Evaluates component heartbeat gaps and determines emergency severity."""

    def evaluate(self, request: OfflineEvaluationRequest) -> OfflineEvaluationResponse:
        alerts: List[OfflineAlert] = []
        for component in request.components:
            alert = self._evaluate_component(component)
            if alert:
                alerts.append(alert)

        return OfflineEvaluationResponse(
            success=True,
            alerts=alerts,
            timestamp=datetime.utcnow().isoformat(),
        )

    def _evaluate_component(self, component: OfflineComponentState) -> Optional[OfflineAlert]:
        gap_seconds = component.gapMs / 1000.0
        severity, likelihood, recommendation, auto_stop = self._score_component(component, gap_seconds)
        if severity == "none":
            return None

        return OfflineAlert(
            componentId=component.componentId,
            severity=severity,
            gapSeconds=round(gap_seconds, 2),
            recommendation=recommendation,
            likelihood=likelihood,
            reason=self._build_reason(component, gap_seconds),
            autoStopRecommended=auto_stop,
            metadata=component.metadata or {},
        )

    def _score_component(
        self,
        component: OfflineComponentState,
        gap_seconds: float,
    ) -> Tuple[str, float, str, bool]:
        criticality = (component.criticality or "low").lower()
        manual = component.manualOffline is True or component.manualOverride is True
        threshold = (component.thresholdMs or 8000) / 1000.0
        heartbeat = (component.heartbeatTimeoutMs or 4000) / 1000.0

        if manual:
            return self._manual_score(criticality)

        if gap_seconds < heartbeat:
            return "none", 0.0, "", False

        if gap_seconds < threshold:
            return "warning", 0.4, "Monitor component connectivity", False

        if criticality == "high":
            return "critical", 0.9, "Prepare/trigger emergency stop", True
        if criticality == "medium":
            return "high", 0.7, "Alert operator and slow production", False
        return "medium", 0.55, "Log issue and plan manual recovery", False

    def _manual_score(self, criticality: str) -> Tuple[str, float, str, bool]:
        if criticality == "high":
            return (
                "critical",
                0.95,
                "Critical component manually disabled – trigger emergency stop",
                True,
            )
        if criticality == "medium":
            return (
                "high",
                0.75,
                "Important component manually disabled – alert operator",
                False,
            )
        return (
            "warning",
            0.4,
            "Non-critical component manually disabled",
            False,
        )

    def _build_reason(self, component: OfflineComponentState, gap_seconds: float) -> str:
        pieces = [f"Heartbeat silent for {gap_seconds:.1f}s"]
        if component.status:
            pieces.append(f"last status '{component.status}'")
        if component.manualOffline:
            pieces.append("manual offline flag detected")
        if component.metadata.get("manualOfflineReason"):
            pieces.append(f"reason: {component.metadata['manualOfflineReason']}")
        if component.metadata.get("lastHeartbeat"):
            pieces.append(f"last heartbeat {component.metadata['lastHeartbeat']}")
        return " | ".join(pieces)


