# SPAD Robot Simulator — Developer Guide

| Field | Value |
|------|--------|
| Status | Approved |
| Software version | 0.1.0 |
| Unity | 2020.3.49f1 |
| CSV schema | v1 |
| Last verified commit | <first stable version> |
| Last review date | 2026-07-21 |
| Owners | SPAD |
| Repo | «https://github.com/RezaSparks/spad-robot-simulator» |

### Doc rules
- Describe **current** behavior only.
- Every path, API, default must match the repo.

## 1. Purpose & audience

### 1.1 Purpose
«One paragraph: what this simulator does.»

### 1.2 Audience
- Contributors

### 1.3 After reading this guide you can
- [ ] Run the demo from a cold clone
- [ ] Place waypoints, generate trajectory, replay, export CSV
- [ ] Understand IK / collision / planning contracts
- [ ] «other»

---

## 2. Quick start (reproducible)
### 2.1 Prerequisites
| Item | Required |
|------|----------|
| Unity Editor | 2020.3.49f1 |
| Git | yes |
| Git LFS | «yes/no» |
| OS tested | «Windows 10/11 / …» |
| Other | «» |

### 2.2 Clone
```bash
git clone https://github.com/RezaSparks/spad-robot-simulator.git
cd spad-robot-simulator

### 2.3 Open
1. Unity Hub → Add → select project root
2. Open with **2020.3.49f1**
3. Open scene: `«Assets/.../SampleScene.unity»`

### 2.4 Run checklist
1. Press Play
2. "how to place waypoint 1"
Right-click anywhere on the work plane (the ground plane) in the Game view.
3. "how to place waypoint 2"
Repeat step 2 – right-click anywhere on the work plane.
4. "how to generate trajectory"
After placing at least 1 waypoints, press the Spacebar key.
5. "how to replay / change speed"
| Action | How |
|--------|-----|
| **Play** | Click **Play** (▶) in the UI |
| **Pause** | Click **Pause** (⏸) during playback |
| **Step Forward** | Click **Step Forward** (⏩) — advances **0.01 s** per click by default |
| **Step Backward** | Click **Step Backward** (⏪) — rewinds **0.01 s** per click by default |
| **Reset to Start** | Click **Stop**, or reset the replay controller |
| **Playback speed** | Use **Playback Speed** slider — range **0.1×–5×**, default **1×** |
| **Waypoint speed (pre-generate)** | Use the **Speed** slider in the UI to set waypoint speeds **before** trajectory generation |

**Notes**
- Playback speed changes **how fast** a generated trajectory is replayed; it does **not** regenerate the path.
- Waypoint speed affects **trajectory generation** (sample spacing / timing), not live replay alone.
- Step size: **0.01 s** per click (forward/back).
6. «how to export CSV» → file location: `«path or dialog»`
Generate a trajectory first (see step 4).
Click the "Export CSV As…" button in the UI.
A Save File Dialog will appear.
Choose a location and filename (default: Trajectory_YYYY-MM-DD_HH-mm-ss.csv).
Click Save.

---

## 3. Product scope

### 3.1 In scope
- 2-DOF SCARA simulation
- FK / IK
- Geometric collision checks
- Trajectory generation + replay
- CSV import/export
- uGUI waypoint editing
- «»

### 3.2 Out of scope
- «»

### 3.3 Tech stack
| Layer | Choice |
|-------|--------|
| Engine | Unity 2020.3.49f1 |
| Language | C# |
| UI | uGUI |
| File dialogs | StandaloneFileBrowser «confirm path» |
| External I/O | CSV only |
| Other packages | «none / list» |

## 4. Repository map

Assets/
  Scenes/
SampleScene.unity
  Scripts/
...
  Prefabs/
...
  ThirdParty/
StandaloneFileBrowser/
docs/
  ...
README.md
DEVELOPER.md
CHANGELOG.md
LICENSE

---

## 5. Conventions & invariants

### 5.1 Units
| Quantity | Unit |
|----------|------|
| Length | meters |
| Angle | degrees |
| Time | seconds |
| Speed | maxDegreesPerSecond |

### 5.2 Frames & coordinates
- World up axis: Y
- Work plane: XZ
- Base origin: robotBase.transform.position
- θ1 zero pose: The first arm link points along the +Z axis (Unity world forward).
- θ1 positive direction: Counter-clockwise (CCW)
- θ2 zero / positive: θ2 = 0 when the arm is fully extended (elbow straight).
- Elbow-up definition: θ2 > 0
- Elbow-down definition: θ2 < 0

### 5.3 Robot invariants
| Name | Value |
|------|--------|
| L1 | 	2.0 meters |
| L2 | 	2.0 meters |
| Joint1 limits | [-160°, +160°] |
| Joint2 limits | [-170°, +170°] |
| Reachable range |	[0.0, 4.0] meters (annulus) |
| Forbidden wedge | 40° wedge behind the base (between +160° and +200°, i.e., -160° to -160° wrapping around 180°). |
| Collision model | Geometric segment-based (2D in XZ plane). |

---

## 6. Architecture

### 6.1 Layers

┌─────────────────────────────────────────────────────────────────────┐
│                         UI Layer                                    │
│            (SCARA_UIController, SCARA_EndEffectorDisplay,           │
│             SCARA_CameraController, SCARA_WaypointRowUI)            │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Application Layer                              │
│   (SCARA_WaypointManager, SCARA_RobotController,                    │
│    SCARA_ReplayController, SCARA_WaypointMarker)                    │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                         Core Layer                                  │
│   (SCARA_IKSolver, SCARA_CollisionChecker, SCARA_TrajectoryPlanner, │
│    SCARA_CSVSerializer, SCARA_CollisionAutoSetup)                   │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                         Unity Engine                                │
│   (Transforms, uGUI, Input, Colliders, File Dialogs via SFB)        │
└─────────────────────────────────────────────────────────────────────┘

### 6.2 Responsibilities

| Component | Owns | Must not do |
|-----------|------|-------------|
| `SCARA_IKSolver` | Pure kinematic math: forward/inverse kinematics, angle unwrapping, joint limits checking | No state, no Unity API calls, no collision logic |
| `SCARA_CollisionChecker` | Geometric collision tests: self-collision, base pillar, obstacle box (segment-circle/segment-AABB) |  obstacle box (segment-circle/segment-AABB)	No IK logic, no state, no Unity Physics components |
| `SCARA_WaypointManager` | Waypoint list (waypoints), marker spawning, selection state, drag logic, planeY for gizmos | No trajectory generation, no playback control |
| `SCARA_TrajectoryPlanner` | 	Trajectory generation: wedge-safe routing, elbow-flip fallback, arc avoidance, sample creation | No persistence, no UI interaction, no waypoint storage |
| `SCARA_RobotController` | 	UI events binding, button/slider actions, status messages, file dialogs (via SFB), waypoint list refresh | No simulation logic, no direct IK/collision calls (delegates to managers) |
| `SCARA_UIController` | Trajectory playback: play/pause, step forward/backward, speed control, time tracking | No trajectory generation, no waypoint management |
| `SCARA_ReplayController` | Trajectory playback: play/pause (Play/Pause/Resume), step forward/backward (StepForward/StepBackward), speed control (speedMultiplier), time tracking (CurrentTime, TotalDuration), OnPlaybackComplete event. | No trajectory generation, no waypoint management, no UI logic. |
| `SCARA_CSVSerializer` | CSV import/export: Save (with metadata + waypoints), TryLoad (parses waypoints and trajectory samples). | No business logic, no UI interaction, no simulation state. |
| `SCARA_CameraController` | Camera orbit (Alt + LMB), pan (MMB), zoom (scroll wheel), OSD display for speed multiplier. | No simulation logic, no waypoint interaction. |
| `SCARA_EndEffectorDisplay` | Real-time display of end-effector position and joint angles on UI texts. | No logic beyond display, no state. |
| `SCARA_WaypointMarker` | Visual representation of a waypoint (TextMesh, renderer color), blink for active marker, hover/selection colors. | No waypoint data storage, no business logic. |
| `SCARA_WaypointRowUI` | UI row for a waypoint (InputFields, Text labels, selection highlight, move up/down buttons). | No business logic, no direct waypoint modification (delegates via events). |
| `SCARA_CollisionAutoSetup` | Auto‑compute collision parameters from visual meshes (ComputeFromMeshes via [ContextMenu]). | No runtime logic (only used in Editor / Start). |

### 6.3 Runtime lifecycle
1. Cold start / OnEnable: «»
2. Edit waypoints: «»
3. Generate: «»
4. Replay: «»
5. Save/Load CSV: «»
6. Clear/reset: «»

### 6.4 Sequence — Generate trajectory
text
User action
  → «UI method»
  → «Planner method»
  → «Collision checks»
  → cache samples in «»
  → UI status «»

### 6.5 Sequence — Export CSV
text
«fill»

### 6.6 Known coupling / debt
| Item | Risk | Mitigation / status |
|------|------|---------------------|
| Duplicated collision params | high | «document sync / future SO» |
| UI → concrete classes | medium | «interfaces later» |
| «» | «» | «» |

---

## 7. Data contracts

### 7.1 `Waypoint`
| Field | Type | Unit | Required | Notes |
|-------|------|------|----------|-------|
| «name» | «» | «» | yes/no | «» |

**Validation rules**
- «»

### 7.2 `TrajectorySample`
| Field | Type | Unit | Required | Notes |
|-------|------|------|----------|-------|
| «time» | float | s | yes | monotonic non-decreasing |
| «theta1» | float | deg | yes | «» |
| «theta2» | float | deg | yes | «» |
| «» | «» | «» | «» | «» |

---

## 8. Scene & configuration

### 8.1 Required scene objects
| Object name | Component(s) | Purpose |
|-------------|--------------|---------|
| RobotBase | Transform | «» |
| Joint1 | Transform, SCARA_RobotController, SCARA_CollisionAutoSetup | «» |
| Joint2 | Transform | «» |
| EndEffector | Transform | «» |
| Plane | Collider  | «» |
| MainCamera | Camera, SCARA_CameraController | «» |
| Manager | SCARA_WaypointManager | «» |
| RobotBase | Transform | «» |
| RobotBase | Transform | «» |


### 8.2 Inspector wiring matrix
| Component | Field | Must reference |
|-----------|-------|----------------|
| «UIController» | «waypointManager» | «scene object» |

### 8.3 Parameter bible
| Param | Default | Unit | Safe range | Consumed by | Sync group |
|-------|---------|------|------------|-------------|------------|
| «armThicknessRadius» | «» | m | «» | Collision, Planner | collision |
| «timeStep» | «0.001» | s | «» | Planner | trajectory |
| «» | «» | «» | «» | «» | «» |

### 8.4 Config sync rules
### 8.5 Config sync rules
□ Create RobotBase GameObject (empty) at world origin.
□ Create Joint1 as child of RobotBase (position at base pivot).
□ Add SCARA_RobotController component to joint 1.
□ Create Joint2 as child of Joint1 (position at arm1 length on +Z).
□ Create EndEffector as child of Joint2 (position at arm2 length on +Z).
□ Assign Joint1, Joint2, EndEffector to RobotController.
□ Create Plane (Plane or Quad) at y=0, add Collider.
□ Create MainCamera, add SCARA_CameraController, assign target.
□ Create Manager GameObject, add SCARA_WaypointManager, add SCARA_TrajectoryPlanner, add SCARA_ReplayController, add SCARA_UIController.
□ Assign targetCamera, planeCollider, robotBase, markerPrefab.
□ Assign joint1, joint2 to ReplayController.
□ Create UICanvas with all UI elements (buttons, sliders, scroll view).
□ Assign all UI references and manager references.
□ Add SCARA_EndEffectorDisplay to canvas, assign ReplayController.
□ Add SCARA_CollisionAutoSetup to RobotBase, assign visuals.
□ Run ComputeFromMeshes() via Context Menu to auto-set collision parameters.
□ Test: right-click to add waypoint, Space to generate trajectory.

---
## 9. Quality standards

### 9.1 Code
- Naming: `SCARA_` prefix for «»
- No unexplained magic numbers
- Null/Inspector validation: «»

### 9.2 PR checklist
- [ ] Code builds in Unity 2020.3.49f1
- [ ] Scene/prefab diffs reviewed
- [ ] Defaults verified
- [ ] Docs updated
- [ ] Tests/fixtures updated
- [ ] CSV schema impact checked
- [ ] CHANGELOG entry if user-visible

### 9.3 Commits
Format: `«type: summary»`  
Types: `feat | fix | docs | refactor | test | chore`

---

## 10. References

- `README.md`
- `CHANGELOG.md`
- `docs/adr/«…»`
- «external notes»

---