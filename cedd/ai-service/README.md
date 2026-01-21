# AI Service (FastAPI)

This service provides anomaly detection, predictive maintenance, and process optimisation APIs for the MINDTWIN dashboard.

## Quick start

```bash
cd cedd/ai-service
python -m venv .venv
.venv\Scripts\activate   # Windows
pip install -r requirements.txt
uvicorn app:app --reload --host 0.0.0.0 --port 5000
```

Configure environment variables in `.env` (optional). Default settings target `http://localhost:3000` for telemetry.

## Endpoints

| Method | Path                     | Purpose                                      |
|--------|--------------------------|----------------------------------------------|
| GET    | `/health`                | Service health/status                        |
| GET    | `/ai/models/status`      | List available models + versions             |
| POST   | `/ai/anomaly/detect`     | Batch anomaly detection                      |
| POST   | `/ai/maintenance/predict`| Predict maintenance windows                  |
| POST   | `/ai/optimize`           | Generate optimisation suggestions            |
| POST   | `/ai/alerts/range`       | Ingest out-of-range alerts for learning      |

The implementation ships with lightweight baseline models (IsolationForest, ARIMA-style trend extrapolation, and heuristic optimisers). You can later plug in richer models without touching the dashboard/backend contracts.


