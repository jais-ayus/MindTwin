from __future__ import annotations

from datetime import datetime
from typing import List, Optional

from schemas import (
    ParameterEvaluation,
    ParameterEvaluationRequest,
    ParameterEvaluationResponse,
    ParameterWarning,
)


class ParameterForecaster:
    """Provides heuristic wear/throughput forecasts for parameter deviations."""

    def __init__(self) -> None:
        self.minimum_rul_hours = 6.0

    def evaluate(self, request: ParameterEvaluationRequest) -> ParameterEvaluationResponse:
        warnings: List[ParameterWarning] = []
        for evaluation in request.evaluations:
            warning = self._evaluate_single(evaluation)
            if warning:
                warnings.append(warning)

        return ParameterEvaluationResponse(
            success=True,
            warnings=warnings,
            timestamp=datetime.utcnow().isoformat(),
        )

    def _evaluate_single(self, evaluation: ParameterEvaluation) -> Optional[ParameterWarning]:
        value = evaluation.proposedValue
        recommended_min = self._fallback(evaluation.recommendedMin, evaluation.minValue)
        recommended_max = self._fallback(evaluation.recommendedMax, evaluation.maxValue)

        if recommended_min is not None and recommended_max is not None:
            if recommended_min <= value <= recommended_max:
                return None

        default_value = evaluation.defaultValue
        deviation_ratio = self._compute_deviation_ratio(value, default_value)
        magnitude = abs(deviation_ratio)

        wear_multiplier = 1.0 + max(magnitude * 1.6, 0.15)
        wear_multiplier = self._adjust_for_bounds(wear_multiplier, value, recommended_min, recommended_max)
        wear_multiplier = round(wear_multiplier, 2)

        risk = self._classify_risk(wear_multiplier)
        throughput_impact = deviation_ratio * 100.0
        estimated_rul = round(max(self.minimum_rul_hours, 72.0 / wear_multiplier), 1)

        notes = self._build_notes(
            deviation_ratio,
            default_value,
            recommended_min,
            recommended_max,
            value,
        )
        suggestions = self._build_suggestions(evaluation, risk, value, recommended_min, recommended_max)

        return ParameterWarning(
            componentId=evaluation.componentId,
            parameter=evaluation.parameter,
            risk=risk,
            throughputImpact=round(throughput_impact, 1),
            wearMultiplier=wear_multiplier,
            estimatedRULHours=estimated_rul,
            notes=notes,
            suggestions=suggestions,
            value=value,
            defaultValue=default_value,
            recommendedRange={"min": recommended_min, "max": recommended_max},
            metadata=evaluation.metadata or {},
        )

    @staticmethod
    def _fallback(primary, secondary):
        return primary if primary is not None else secondary

    @staticmethod
    def _compute_deviation_ratio(value: float, default_value: Optional[float]) -> float:
        if default_value in (None, 0):
            return 0.0
        return (value - default_value) / default_value

    @staticmethod
    def _adjust_for_bounds(
        wear_multiplier: float,
        value: float,
        recommended_min: Optional[float],
        recommended_max: Optional[float],
    ) -> float:
        adjusted = wear_multiplier
        if recommended_min is not None and value < recommended_min and recommended_min != 0:
            adjusted += (recommended_min - value) / abs(recommended_min) * 0.5
        if recommended_max is not None and value > recommended_max and recommended_max != 0:
            adjusted += (value - recommended_max) / abs(recommended_max) * 0.5
        return max(adjusted, 1.05)

    @staticmethod
    def _classify_risk(wear_multiplier: float) -> str:
        if wear_multiplier >= 2.0:
            return "critical"
        if wear_multiplier >= 1.5:
            return "high"
        if wear_multiplier >= 1.2:
            return "medium"
        return "low"

    @staticmethod
    def _build_notes(
        deviation_ratio: float,
        default_value: Optional[float],
        recommended_min: Optional[float],
        recommended_max: Optional[float],
        value: float,
    ) -> List[str]:
        notes: List[str] = []
        if default_value not in (None, 0):
            percent = deviation_ratio * 100
            notes.append(f"Deviation {percent:.1f}% from default ({default_value})")
        if recommended_min is not None and value < recommended_min:
            notes.append(f"Below recommended minimum ({recommended_min})")
        if recommended_max is not None and value > recommended_max:
            notes.append(f"Above recommended maximum ({recommended_max})")
        return notes

    @staticmethod
    def _build_suggestions(
        evaluation: ParameterEvaluation,
        risk: str,
        value: float,
        recommended_min: Optional[float],
        recommended_max: Optional[float],
    ) -> List[str]:
        suggestions: List[str] = []
        if recommended_min is not None and value < recommended_min:
            suggestions.append(f"Increase towards {recommended_min} to stabilize throughput")
        if recommended_max is not None and value > recommended_max:
            suggestions.append(f"Reduce closer to {recommended_max} to limit wear")
        if risk in {"high", "critical"}:
            suggestions.append("Schedule maintenance inspection within the next shift")
        if evaluation.context.get("source") != "telemetry":
            suggestions.append("Confirm PLC parameters align with dashboard settings")
        return suggestions










