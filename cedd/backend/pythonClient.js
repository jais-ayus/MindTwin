const axios = require('axios');

class PythonClient {
    constructor() {
        this.baseUrl = process.env.PYTHON_AI_URL || 'http://localhost:5000';
        this.health = {
            status: 'unknown',
            lastSuccess: null,
            lastError: null,
            errorMessage: null
        };
        this.http = axios.create({
            baseURL: this.baseUrl,
            timeout: parseInt(process.env.PYTHON_AI_TIMEOUT_MS || '6000', 10)
        });
    }

    async get(path, params = {}) {
        try {
            const res = await this.http.get(path, { params });
            this._markHealthy();
            return res.data;
        } catch (err) {
            this._markUnhealthy(err);
            throw err;
        }
    }

    async post(path, payload = {}) {
        try {
            const res = await this.http.post(path, payload);
            this._markHealthy();
            return res.data;
        } catch (err) {
            this._markUnhealthy(err);
            throw err;
        }
    }

    _markHealthy() {
        this.health = {
            status: 'healthy',
            lastSuccess: Date.now(),
            lastError: this.health.lastError,
            errorMessage: null
        };
    }

    _markUnhealthy(err) {
        this.health = {
            status: 'degraded',
            lastSuccess: this.health.lastSuccess,
            lastError: Date.now(),
            errorMessage: err?.message || 'Unknown error'
        };
    }
}

module.exports = new PythonClient();


