### MindTwin

MindTwin is a Unity-based digital twin and IoT experimentation environment. It combines a realvirtual-powered factory simulation, a WebGL-based HMI/dashboard, a Node.js backend API, and a Python (FastAPI) AI service for anomaly detection, predictive maintenance, and optimisation.

This repository hosts:
- The Unity project (Unity **2022.3.62f1**) under `Assets/`, `Packages/`, and `ProjectSettings/`
- A WebGL build that can be opened directly from a browser (`build/` and `cedd/Build/` + `cedd/index.html`)
- The CEDD dashboard + backend (`cedd/`), and AI service (`cedd/ai-service/`)

#### Quick start

- **Unity project**
  - Open the folder in Unity **2022.3.62f1** (see `ProjectSettings/ProjectVersion.txt`)
  - Load the main scene, then play or rebuild WebGL as needed

- **CEDD WebGL dashboard & backend**
  - See `cedd/README.md` for a single-command launch that starts:
    - Node backend API on port 3000
    - Web server on port 8081 serving the Unity WebGL build and dashboard

- **AI service (FastAPI)**
  - See `cedd/ai-service/README.md` for environment setup and endpoints
  - Configure a local `.env` file (see `.env.example`) to point at the backend API if you change ports or hostnames

#### Repository

GitHub: `https://github.com/jais-ayus/MindTwin.git`

