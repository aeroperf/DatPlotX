# Sample Data

Datasets for trying out DatPlotX's three plot modes without your own data.

| File | Plot mode | Description |
|------|-----------|-------------|
| [`XPlaneData.txt`](XPlaneData.txt) | **Stacked Panes** | X-Plane flight simulator data output (`Data.txt` format) — a full simulated flight with ~70 parameters: airspeed, altitude, attitude, G-loads, forces/moments, gear position, and more. Import via `File → Import Data`; the X-Plane format is detected automatically. |
| [`TakeoffForcesAccel.csv`](TakeoffForcesAccel.csv) | **Compact Plot Surface** | Smartphone IMU log recorded during an airline takeoff — accelerations, G-forces, barometric pressure, magnetometer, and attitude vs. time. |
| [`pump-performance-map.csv`](pump-performance-map.csv) | **Grouped Parameter Plot** | Synthetic centrifugal pump performance map generated from pump affinity laws. Inputs: `Speed_RPM` (4 motor speeds) × `Impeller_mm` (3 trims) = 12 curve groups. Sweep `Flow_m3h` against `Head_m`, `Efficiency_pct`, `Power_kW`, or `NPSHr_m`. |

All files are either synthetic or recorded from simulators/consumer sensors — no
proprietary or flight-test data.
