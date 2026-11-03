# AcrophobiaVR
### An Immersive Virtual Reality Environment for Investigating Height-Related Fear Responses

> A Unity 6 based Virtual Reality simulation developed as part of a research internship at IIT Kharagpur to study behavioural responses to height exposure in controlled virtual environments.

---

## Overview

AcrophobiaVR is an immersive Virtual Reality application designed to simulate progressive height exposure scenarios inside a multi-level tower environment. The project was developed to support research into acrophobia (fear of heights) by providing a safe, repeatable, and controllable experimental platform.

Participants navigate through increasingly challenging environments while behavioural events and interaction telemetry are recorded for post-session analysis.

The application was built using **Unity 6**, **Meta XR SDK**, and the **XR Interaction Toolkit**, targeting the **Meta Quest 3** standalone VR headset.

---

## Features

- 🏗 Progressive multi-tier tower environment
- 🌉 Glass bridge and high-altitude traversal challenges
- 🥽 Native Meta Quest 3 support
- 🎮 Continuous joystick locomotion
- ✋ Natural VR interactions using XR Interaction Toolkit
- 🌫 Dynamic environmental effects (fog, wind, ambience)
- 🎵 Adaptive spatial audio system
- 📊 Behavioural telemetry logging
- 📁 Automatic session export (CSV + metadata)
- 🔬 Research-oriented experiment workflow

---

## Experimental Workflow

1. Enter Participant ID
2. Configure session
3. Begin VR simulation
4. Progress through tower tiers
5. Traverse height challenges
6. Behavioural events are logged automatically
7. Export complete session data

---

## Technology Stack

| Category | Technologies |
|----------|--------------|
| Engine | Unity 6 |
| Language | C# |
| VR SDK | Meta XR SDK |
| Interaction | XR Interaction Toolkit |
| Rendering | Universal Render Pipeline (URP) |
| UI | TextMeshPro |
| Platform | Meta Quest 3 |
| Data Export | CSV + JSON |

---

## Project Structure

```
Assets/
│
├── Scripts/
│   ├── Managers/
│   ├── Interaction/
│   ├── Telemetry/
│   ├── UI/
│   ├── Audio/
│   └── Environment/
│
├── Prefabs/
├── Scenes/
├── Materials/
├── Audio/
├── XR/
└── Resources/
```

---

## Telemetry

The system records participant interaction data throughout the session, including:

- Session start/end
- Tier progression
- Teleport events
- Bridge entry/exit
- Participant decisions
- Time spent per tier
- Custom behavioural markers

Each session generates:

```
Session/
│
├── events.csv
├── metadata.json
└── notes.txt
```

These files can be exported directly from the Meta Quest device for further statistical analysis.

---

## Screenshots

#### To Be Added Soon...

---

## Installation

### Clone Repository

```bash
git clone https://github.com/Rudra-RNC/Acrophobia.git
```

Open the project using **Unity 6**.

---

## Requirements

### Hardware

- Meta Quest 3
- VR Ready PC (for development)

### Software

- Unity 6
- Meta XR SDK
- XR Interaction Toolkit
- Universal Render Pipeline

---

## Running the Project

1. Open the project in Unity.
2. Install all required packages.
3. Switch platform to Android.
4. Connect Meta Quest 3.
5. Build and Run.

---

## Research Context

This project was developed during a research internship at **IIT Kharagpur** to investigate behavioural responses in immersive virtual environments simulating height exposure.

The simulation provides a controlled and repeatable experimental setting while enabling researchers to collect interaction telemetry for later analysis.

---

## Future Improvements

- Eye-tracking integration
- Physiological sensor integration
- Heart rate variability analysis
- Adaptive exposure therapy
- AI-assisted participant behaviour modelling
- Cloud-based telemetry dashboard
- Multi-user experimental sessions

---

## Repository

```
AcrophobiaVR
├── Assets
├── Packages
├── ProjectSettings
├── UserSettings
└── README.md
```

---

## Author

**Rudra**
Research Intern
Indian Institute of Technology Kharagpur

GitHub: https://github.com/Rudra-RNC

---

## Acknowledgements

- IIT Kharagpur
- Unity Technologies
- Meta Reality Labs
- XR Interaction Toolkit Team
- Meta XR SDK

---

## License

This repository is intended for academic and research purposes.

Please contact the author before using any part of this project in publications or derivative research.

---

### ⭐ If you found this project interesting, consider giving it a star!
