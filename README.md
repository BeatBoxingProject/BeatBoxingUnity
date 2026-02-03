# 🚀 Unity Simulation & Game Logic

This folder contains the **Unity project** responsible for the visual frontend and game logic of the *Beat Boxing* mixed-reality system. It receives real-time tracking data from the Python backend, handles the game loop, and renders the projection mapping output.

## 🎮 System Overview

While the Python backend handles the heavy lifting of computer vision, Unity serves as the **Game Engine** and **Renderer**.

1.  **UDP Listener:** Receives 3D coordinate strings for both hands.
2.  **Projection Manager:** Renders high-contrast visuals (targets, particles, UI) against a black background, specifically designed for projection onto a physical cylinder (the boxing bag).
3.  **Game Logic:** Spawns targets and detects collisions using Unity's physics engine.

## 📂 Key Scripts

The core logic is contained in `Assets/Scripts/`:

| Script | Description |
| :--- | :--- |
| **`DualHandReceiver.cs`** | **The Network Bridge.** Listens for UDP packets on Port 5005. Parses the split-string data (`L\|R`) and smooths the movement of the virtual gloves to match the physical player. |
| **`TargetSpawner.cs`** | **The Game Loop.** Automatically spawns targets around the `BagCenter`. It calculates polar coordinates (angle/height) to ensure targets always appear on the surface of the bag. |
| **`TargetHit.cs`** | **Collision Logic.** Attached to the target prefabs. Detects when an object tagged `PlayerHand` enters the trigger zone, plays a particle effect, and increments the score. |
| **`ScoreDisplay.cs`** | **UI Manager.** Simple script to update the TextMeshPro UI with the current global score. |

## 🔌 Integration with Python

This project is pre-configured to listen to the `BeatBoxingPython` backend.

* **Protocol:** UDP
* **Port:** `5005` (Configurable in `DualHandReceiver` inspector)
* **Data Format:** Expects a UTF-8 string: `"Lx,Ly,Lz|Rx,Ry,Rz"`

### Coordinate Mapping
The physical cameras and Unity use different coordinate systems. The `DualHandReceiver` script handles the translation:

* **Scale Multiplier:** Converts the raw meter-based input from Python into Unity world units (default: `1.0`).
* **Offset:** Centers the tracked coordinates onto the virtual bag position.
* **Axis Flipping:** Booleans (`flipX`, `flipY`, `flipZ`) are available in the Inspector to correct mirroring issues instantly.

## 🛠 Scene Setup (`Main.unity`)

The scene is optimized for Mixed Reality projection:

* **`ProjectorCam`:** The main camera. It renders the game view. In a full deployment, this camera's view is projected onto the physical bag.
* **`GloveManager`:** An empty GameObject that holds the `DualHandReceiver` script.
    * *LeftHandObject:* Linked to the `GloveLeft` sphere.
    * *RightHandObject:* Linked to the `GloveRight` sphere.
* **`BagCenter`:** The pivot point around which targets spawn. This must be aligned with the center of your physical bag in the virtual world.
* **`Canvas`:** Displays the score. Ensure this is positioned where it is visible on the projection.

## ⚙️ Configuration & Calibration

To calibrate the virtual world to your physical setup:

1.  **Open the Scene:** Load `Assets/Scenes/Main.unity`.
2.  **Select `GloveManager`:**
    * **Visual Calibration:** Expand the `GloveManager` in the Hierarchy and **enable the MeshRenderer** on the `GloveLeft` and `GloveRight` children. This makes the spheres visible, allowing you to visually align them with your physical gloves.
    * **Port:** Ensure this matches your Python script (Default `5005`).
    * **Scale Multiplier:** Adjust this if the virtual hands move too little or too much compared to real life.
    * **Offset:** Adjust X/Y/Z to make the virtual hands align with the physical bag location.
    * **Smoothing:** Set the value closer to 1 to reduce jitter (making movement smoother but slightly delayed), or closer to 0 for instant responsiveness (making movement faster but potentially shakier).
3.  **Select `GameManager` (TargetSpawner):**
    * **Bag Radius:** Measure your physical boxing bag radius and input it here (e.g., `0.19` for a 38cm diameter bag).
    * **Bag Height:** Measure your physical boxing bag height and input it here (e.g., `0.7` for a 70cm bag).
    * **Spawn Angle Range:** Controls how far around the bag targets can appear (e.g., `90` degrees).

## 🚀 How to Run

1.  **Start the Backend:** Ensure your Python tracking script (`main.py`) is running and streaming data.
2.  **Projector Setup & Alignment:**
    * **Open Window:** Open a new **Game** window in the Unity Editor.
    * **Set Display:** In the top-left dropdown of this new window, set the output to **Display 2**.
    * **Position:** Drag this window onto the screen/monitor that the projector projects to and maximize it.
    * **Align Camera:** With the projection visible on the bag, go to the **Scene View** and move/rotate the `ProjectorCam`. Adjust the position and field of view until the virtual bag aligns perfectly with the physical punching bag.
3.  **Play in Unity:** Press the **Play** button in the Unity Editor.
4.  **Punch!:** Hit the red sphere targets with your colered gloves on.
    * *Success:* Explosion particles appear, sound plays, score increases.
    * *Debug:* If hands are moving but not hitting targets, check the Z-depth offset in the `GloveManager`.

## 📦 Requirements

* Unity 6000.3.0f1 or newer.
* TextMeshPro (usually included).
* Universal Render Pipeline (URP) recommended for performant particle effects on projectors.
