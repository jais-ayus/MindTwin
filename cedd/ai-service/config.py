from functools import lru_cache
from pydantic import Field
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    app_name: str = "MINDTWIN AI Service"
    api_version: str = "0.1.0"
    backend_base_url: str = Field("http://localhost:3000", env="BACKEND_BASE_URL")
    telemetry_limit: int = Field(250, env="TELEMETRY_LIMIT")
    anomaly_default_threshold: float = Field(0.7, env="ANOMALY_THRESHOLD")
    maintenance_default_hours: int = Field(72, env="MAINTENANCE_LOOKAHEAD_HOURS")
    optimizer_default_horizon_minutes: int = Field(30, env="OPTIMIZER_HORIZON_MINUTES")

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


@lru_cache
def get_settings() -> Settings:
    return Settings()


settings = get_settings()

