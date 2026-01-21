from __future__ import annotations

from dataclasses import dataclass
from typing import Dict

from schemas import ComponentTelemetry


@dataclass
class FeatureVector:
    mean: float
    std: float
    z_score: float


def build_feature_vector(component: ComponentTelemetry) -> FeatureVector:
    history_mean = float(component.metadata.get("historyMean", component.value or 0.0))
    history_std = float(component.metadata.get("historyStd", 1.0))
    value = float(component.value or 0.0)
    z_score = 0.0 if history_std == 0 else (value - history_mean) / history_std
    return FeatureVector(mean=history_mean, std=history_std or 1.0, z_score=z_score)


@dataclass
class Trend:
    rate: float
    intercept: float


def rolling_trend(component: ComponentTelemetry) -> Trend:
    history = component.metadata.get("history", [])
    if not history:
        return Trend(rate=0.0, intercept=float(component.value or 0.0))

    indices = list(range(len(history)))
    values = history
    count = float(len(indices))
    mean_x = sum(indices) / count
    mean_y = sum(values) / count

    numerator = sum((x - mean_x) * (y - mean_y) for x, y in zip(indices, values))
    denominator = sum((x - mean_x) ** 2 for x in indices) or 1.0
    rate = numerator / denominator
    intercept = mean_y - rate * mean_x
    return Trend(rate=rate, intercept=intercept)


def clamp(value: float, min_value: float, max_value: float) -> float:
    return max(min_value, min(value, max_value))


