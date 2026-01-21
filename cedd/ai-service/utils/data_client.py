from __future__ import annotations

from typing import List

import httpx

from config import settings
from schemas import ComponentTelemetry, TelemetryEnvelope


class BackendDataClient:
    def __init__(self) -> None:
        self.base_url = settings.backend_base_url.rstrip("/")

    def fetch_recent_components(self, limit: int | None = None) -> List[ComponentTelemetry]:
        params = {"limit": limit or settings.telemetry_limit}
        url = f"{self.base_url}/api/telemetry"

        try:
            resp = httpx.get(url, params=params, timeout=5.0)
            resp.raise_for_status()
            payload = resp.json()
            envelope = TelemetryEnvelope(**payload)
            return envelope.components
        except Exception:
            return []


backend_client = BackendDataClient()


