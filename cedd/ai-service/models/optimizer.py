from __future__ import annotations

from datetime import datetime
from typing import Dict, List

from config import settings
from schemas import (
    ComponentTelemetry,
    OptimizationRequest,
    OptimizationResponse,
    OptimizationSuggestion,
)
from utils.feature_engineering import clamp


class ProcessOptimizer:
    def optimise(self, request: OptimizationRequest) -> OptimizationResponse:
        suggestions: List[OptimizationSuggestion] = []
        horizon = request.horizonMinutes or settings.optimizer_default_horizon_minutes

        for component in request.components:
            suggestion = self._optimise_component(component, request.objective, horizon)
            if suggestion:
                suggestions.append(suggestion)

        return OptimizationResponse(
            success=True,
            suggestions=suggestions,
            timestamp=datetime.utcnow().isoformat(),
        )

    def _optimise_component(
        self, component: ComponentTelemetry, objective: str, horizon: int
    ) -> OptimizationSuggestion | None:
        parameter = "TargetSpeed"
        current = component.metadata.get(parameter) or component.value
        if current is None:
            return None

        max_safe = component.metadata.get("maxSpeed", current * 1.2)
        min_safe = component.metadata.get("minSpeed", current * 0.5)

        delta = 0.0
        if objective == "energy":
            delta = -0.1 * current
        else:
            delta = 0.15 * current

        recommended = clamp(current + delta, min_safe, max_safe)

        expected = self._impact(current, recommended, objective, horizon)
        explanation = (
            f"Adjusting {component.name} by {recommended-current:+.1f} targets "
            f"{objective} improvement while respecting [{min_safe}, {max_safe}]."
        )

        return OptimizationSuggestion(
            componentId=component.name,
            parameter=parameter,
            current=round(float(current), 2),
            recommended=round(float(recommended), 2),
            constraintsRespected=True,
            expectedImpact=expected,
            explanation=explanation,
        )

    def _impact(
        self, current: float, recommended: float, objective: str, horizon: int
    ) -> Dict[str, float]:
        delta = recommended - current
        if objective == "energy":
            return {
                "energy": round(-delta / max(current, 1e-3) * 8, 2),
                "throughput": round(delta / max(current, 1e-3) * -2, 2),
            }
        return {
            "throughput": round(delta / max(current, 1e-3) * 12, 2),
            "energy": round(delta / max(current, 1e-3) * 3, 2),
            "horizonMinutes": horizon,
        }


