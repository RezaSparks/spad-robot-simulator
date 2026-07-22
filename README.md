# 2-DOF SCARA Robot Simulator

<p align="center">
  <img src="Assets/Textures/LOGO.png" alt="LOGO" width="120">
</p>

<p align="center">
  <b>An interactive 2-DOF SCARA robot simulator built with Unity</b><br>
  Waypoint-based trajectory planning · Inverse kinematics · Real-time replay · CSV export/import
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#requirements">Requirements</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="#usage">Usage</a> •
  <a href="#architecture">Architecture</a> •
  <a href="#csv-format">CSV Format</a>
</p>

## Features

*   **Interactive Waypoint Placement** — Right-click anywhere on the work plane to place waypoints. The simulator automatically solves inverse kinematics and displays joint angles in real time.
*   **Trajectory Generation** — Press Spacebar to generate a smooth, time-optimal joint-space trajectory across all waypoints. The planner respects joint limits, forbidden zones, and collision constraints.
*   **Real-Time Replay** — Play, pause, step forward/backward, and scrub through trajectories at variable playback speeds (0.1×–5×).
*   **Collision Detection** — Geometric self-collision checks, base-pillar proximity tests, and obstacle-box intersection detection keep the robot within safe operating envelopes.
*   **CSV Export / Import** — Save trajectories as structured CSV files with full metadata, waypoint definitions, and time-series samples. Reload previously saved sessions with one click.
*   **Editable Waypoints** — Enter Edit Mode to drag waypoints directly in the scene, reorder them in the UI list, or adjust per-waypoint speeds before generation.
*   **Camera Controls** — Orbit (Alt + LMB), pan (MMB), and zoom (scroll wheel) with adjustable speed multipliers and on-screen display feedback.
*   **Visual Gizmos** — Toggle joint-limit arcs, reachable-area rings, collision radii, and obstacle boxes directly in the Scene view.

## Requirements

| Item             | Required                        |
| ---------------- | ------------------------------- |
| **Unity Editor** | 2020.3.49f1 LTS                 |
| **OS**           | Windows 10 / Windows 11         |
| **Git**          | Yes                             |
| **Git LFS**      | No                              |
| **GPU**          | DX11-compatible                 |
| **RAM**          | 4 GB minimum (8 GB recommended) |

## Quick Start

###  **Clone the repository**
    ```bash
    git clone https://github.com/RezaSparks/spad-robot-simulator.git
    cd spad-robot-simulator
### Open in Unity

1. Launch Unity Hub.
2. Click **Add** → select the `spad-robot-simulator` project folder.
3. Open the project with Unity `2020.3.49f1`.
4. Open the scene: `Assets/Scenes/SampleScene.unity`.

### Run the simulator

1. Press **Play** in the Editor.
2. Right-click on the ground plane in the **Game** view to place your first waypoint.
3. Place additional waypoints as desired.
4. Press **Spacebar** to generate the trajectory.
5. Click **Play (▶)** to start replay.

---

## Usage

### Placing Waypoints

| Action                | How                                                                    |
| --------------------- | ---------------------------------------------------------------------- |
| **Add waypoint**      | Right-click on the work plane (ground collider)                        |
| **Remove waypoint**   | Select the waypoint row in the left panel and click the remove button  |
| **Reorder waypoints** | Use the ↑ / ↓ arrows in each waypoint row                              |
| **Drag waypoint**     | Enter **Edit Mode** (top-right button), then drag markers in the scene |
| **Adjust speed**      | Select a waypoint row and use the **Speed** slider (default: 50%)      |

### Trajectory Controls

| Action                  | How                                                         |
| ----------------------- | ----------------------------------------------------------- |
| **Generate trajectory** | Press `Spacebar` or click the generate button               |
| **Export CSV**          | Click **Export CSV As…** — choose a filename and location   |
| **Load CSV**            | Click **Load** — select a previously exported `.csv` file   |
| **Clear all**           | Click **Clear** to remove all waypoints and trajectory data |
| **Undo**                | Click **Undo** to revert the last waypoint action           |

### Playback Controls

| Action             | How                                                                   |
| ------------------ | --------------------------------------------------------------------- |
| **Play / Pause**   | Click **Play** (▶) / **Pause** (⏸)                                    |
| **Step Forward**   | Click **Step Forward** (⏩) — advances **0.01 s** per click            |
| **Step Backward**  | Click **Step Backward** (⏪) — rewinds **0.01 s** per click            |
| **Playback speed** | Use the **Playback Speed** slider — range **0.1×–5×**, default **1×** |

> **Note:** Playback speed affects replay speed only; it does not regenerate the trajectory. Waypoint speed affects trajectory generation (sample spacing / timing).

### Camera Controls

| Action               | Input                            |
| -------------------- | -------------------------------- |
| **Orbit**            | Alt + Left Mouse Button          |
| **Pan**              | Press Middle Mouse Button + drag |
| **Zoom**             | Mouse Scroll Wheel               |
| **Speed multiplier** | Ctrl + Scroll Wheel (0.1×–5×)    |

---

## Architecture

The simulator is organized into four architectural layers:
┌─────────────────────────────────────────────────────────────┐
│                        UI Layer                             │
│   SCARA_UIController · SCARA_EndEffectorDisplay             │
│   SCARA_CameraController · SCARA_WaypointRowUI              │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Application Layer                         │
│   SCARA_WaypointManager · SCARA_RobotController             │
│   SCARA_ReplayController · SCARA_WaypointMarker             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      Core Layer                             │
│   SCARA_IKSolver · SCARA_CollisionChecker                   │
│   SCARA_TrajectoryPlanner · SCARA_CSVSerializer             │
│   SCARA_CollisionAutoSetup                                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     Unity Engine                            │
│   Transforms · uGUI · Input · Colliders · SFB               │
└─────────────────────────────────────────────────────────────┘

### Key Components

| Component                 | Responsibility                                               |
| ------------------------- | ------------------------------------------------------------ |
| `SCARA_WaypointManager`   | Waypoint list, marker spawning, selection, drag logic        |
| `SCARA_TrajectoryPlanner` | Trajectory generation with collision-aware routing           |
| `SCARA_ReplayController`  | Playback control, time tracking, step navigation             |
| `SCARA_RobotController`   | Joint driving, gizmo visualization, collision state feedback |
| `SCARA_UIController`      | Button/slider event binding, status messages, file dialogs   |
| `SCARA_CameraController`  | Orbit, pan, zoom, OSD speed display                          |
| `SCARA_IKSolver`          | Pure math: forward/inverse kinematics, angle unwrapping      |
| `SCARA_CollisionChecker`  | Geometric tests: segment-circle, segment-AABB                |
| `SCARA_CSVSerializer`     | CSV import/export with metadata and waypoint headers         |

---

Robot Specifications
| Parameter              | Value                               |
| ---------------------- | ----------------------------------- |
| **Degrees of Freedom** | 2 (planar)                          |
| **Arm Length L1**      | 1.845999 m                          |
| **Arm Length L2**      | 1.603999 m                          |
| **Joint 1 Limits**     | −160° to +160°                      |
| **Joint 2 Limits**     | −170° to +170°                      |
| **Reachable Range**    | Annulus \[0.0, 4.0] m               |
| **Forbidden Wedge**    | 40° behind base (+160° to +200°)    |
| **Work Plane**         | XZ (world up: +Y)                   |
| **θ₁ Zero Pose**       | First link points along +Z          |
| **θ₂ Zero Pose**       | Arm fully extended (elbow straight) |
| **Elbow-up**           | θ₂ > 0°                             |
| **Elbow-down**         | θ₂ < 0°                             |

---

## CSV Format

Exported trajectory files follow a strict schema with metadata headers, waypoint definitions, and time‑series samples.

```csv
# ROBOT PARAMS: Arm2=1.6 MaxSpeed=180 Samples=12253
# Generated: 2026-07-21 15:38:47
# WAYPOINTS:
# WP: 0.15, 2.93, 32.2708, -63.667, 50
# WP: -0.6, 2.83, 16.7442, -65.271, 50
# WP: -1.3, 2.58, 2.34119, -65.046, 50
# WP: -1.8, 2.07, -9.5264, -71.526, 50
# WP: -2.4, 1.43, -26.104, -72.057, 50
# WP: -2.9, 0.68, -49.45, -59.47, 50
#
Time,X,Z,Theta1,Theta2
0,0,3.45,0,0
0.001,0.00015,3.45,0.03041,-0.06
0.002,0.0003,3.45,0.06082,-0.12
...
```

spad-robot-simulator/
├── Assets/
│   ├── Documentation/          # Project docs and reference images
│   ├── Editor/                 # Custom inspectors and editor tools
│   ├── Materials/              # Surface materials
│   ├── Models/                 # Native Unity meshes
│   ├── ModelsFBX/              # Imported FBX assets
│   ├── Prefabs/                # Reusable GameObject templates
│   ├── Scenes/
│   │   └── SampleScene.unity   # Primary simulation scene
│   ├── Scripts/                # C# runtime and editor scripts
│   ├── StandaloneFileBrowser/  # Third-party file dialog plugin
│   ├── Textures/               # UI sprites and environment textures
│   └── Trajectories/           # Default CSV export location
├── docs/                       # Additional documentation
├── README.md                   # This file
├── DEVELOPER.md                # Internal developer guide
├── CHANGELOG.md                # Version history
└── LICENSE                     # License terms

Development
Building from Source
Ensure Unity 2020.3.49f1 is installed via Unity Hub.
Clone the repo and open the project root.
Open Assets/Scenes/SampleScene.unity.
Press Play to run in the Editor, or build via File → Build Settings.
Running Tests
Currently, the project relies on manual integration testing through the Editor Play Mode. Automated test fixtures are planned for a future release.
Contributing
Contributions are welcome! Please follow these steps:
Fork the repository.
Create a feature branch (git checkout -b feature/amazing-feature).
Commit your changes with clear messages (feat:, fix:, docs:, refactor:).
Push to your branch.
Open a Pull Request.
Please ensure your code builds in Unity 2020.3.49f1 and does not break existing scene references.

License
This project is proprietary software owned by SPAD. See LICENSE for details.

Acknowledgments
Built with Unity 2020.3 LTS
File dialogs powered by StandaloneFileBrowser
Developed by the SPAD Engineering Team

<p align="center">
  <sub>Made with precision by <a href="https://github.com/RezaSparks">SPAD</a></sub>
</p>
