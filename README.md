# Mohr's Circle and Stress Tensor Visualizer

[AI Disclaimer: vibe-coded project using ChatGPT Codex]

A calculator and visualizer for Mohr's Circle and the corresponding stress tensor diagram. In other words, a WPF desktop app for visualizing plane-stress transformations with Mohr's circle and a rotating stress tensor diagram.

## Visualizer Engine Choice

This repository uses C# and WPF with native `Canvas` drawing primitives. For this application, WPF is preferable to a game or plotting engine because the core experience is interactive vector geometry: draggable points, editable labels, text boxes, menus, dialogs, screenshots, and Windows Help integration. WPF also keeps the app easy to package as a normal Windows desktop tool.

If this later grows into a cross-platform or browser-based tool, the best alternative would be TypeScript with SVG/Canvas in a desktop wrapper such as Tauri or Electron. For a Windows-first engineering app, C# and WPF are still the most direct fit.

## Features

MCSTVisualizer 1.1.2:
- Adds `tau_allow` and safety factor `n` inputs for allowable stress checks.
- Highlights resultant stress values in red when `abs(value) > tau_allow / n`.
- Extends the same allowable-stress color scheme to the engineering HTML report.
- Adds right-click drag rotation for both stress tensor diagrams:
  - 2D tensor: horizontal right-drag rotates the displayed plane angle.
  - 3D tensor: right-drag orbits the model with CAD-style yaw/pitch behavior.
- Improves 3D rotation editing:
  - XYZ rotation values are editable text boxes.
  - typed values support two decimal places,
  - mouse/arc dragging still snaps to zero within +/- 2 degrees,
  - circular XYZ rotation arcs avoid wraparound snapping from zero to 180 degrees.

MCSTVisualizer 1.1.1:
- Adds a `Report` button and `File -> Report` command.
- Exports a print-ready engineering HTML report.
- Includes 2D inputs, transformed 2D resultants, 3D tensor inputs, the 3D tensor matrix, principal stresses, maximum shear stress, and mean stress.
- Highlights resultant and principal stress rows for review and printing.
- Release publishing is now configured as a self-contained single-file Windows x64 executable for distribution.

MCSTVisualizer 1.1:
- 3D mode:
  - Adds a "3D" tab next to the original 2D implementation
  - Displays an orthogonal 3D stress tensor diagram together with a "Mohr's Sphere" diagram
  - Adds `sigma_z`, `tau_yz`, `tau_zx` principal stress parameters
  - Adds `sigma1`, `sigma2`, and `sigma3` to the Mohr's Sphere 
  - Implements tri-axis rotation with three draggable rotation arcs around the 3D stress tensor
  - Diagram rotation can also be controlled by three sliders

MCSTVisualizer 1.0:
- Editable plane-stress inputs: `sigma_x`, `sigma_y`, `tau_xy`.
- Computed values: `sigma_ave`, `R`, `tau_max`, `sigma_max`, and `sigma_min`.
- Clickable stress-axis labels so the plane can be changed, for example `xy`, `xz`, `yz`, `zx`, `zy`, or a custom pair.
- Interactive Mohr's circle:
  - drag `sigma_max` and `sigma_min` to change center/radius,
  - drag `tau_max` to change radius,
  - drag the angle line endpoint to change the displayed physical angle.
- Rotatable stress tensor diagram with transformed `sigma_x'`, `sigma_y'`, and `tau_x'y'`.
- File menu with Open, Save, and Save As.
- Image export as PNG, JPEG/JPG, TIFF.
- CSV export for human-readable parameters with unit labels.
- Print-ready engineering report export with highlighted resultant and principal stresses.
- Settings, About, and README-based Help integration.

## Build

# Developers:
Install the .NET Desktop SDK for Windows, then run these from the repo root:

```powershell
dotnet build .\MCSTVisualizer.sln [-c Release]
dotnet run --project .\src\MCSTVisualizer\MCSTVisualizer.csproj
dotnet publish .\src\MCSTVisualizer\MCSTVisualizer.csproj -c Release
```

# Users:
The ready-made standalone executable can be run from `src\MCSTVisualizer\bin\Release\net10.0-windows\win-x64\publish\MCSTVisualizer.exe`. It is published as a self-contained Windows x64 app, so the target PC does not need the .NET Desktop Runtime installed.

## Notes

- `Help -> Help` opens this README file.
- `Edit -> Settings`, `View -> Zoom`, and `View -> Screenshot` are included where requested. Screenshot export is implemented through the menu and the camera button.
- Saved CSV files can be reopened with `File -> Open`.

## Implemented Features

- Add stress invariants and optional strain transformation mode.
- Add a polar grid and snap-to-angle increments on Mohr's circle.
- Add units support with MPa/ksi/psi presets.

## Suggested Future Improvements

- Add examples for common loading cases.
- Add automated tests for stress-transformation formulas.

