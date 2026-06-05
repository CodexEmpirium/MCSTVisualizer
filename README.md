# Mohr's Circle and Stress Tensor Visualizer

[AI Disclaimer: vibe-coded project using ChatGPT Codex]

A calculator and visualizer for Mohr's Circle and the corresponding stress tensor diagram. In other words, a WPF desktop app for visualizing plane-stress transformations with Mohr's circle and a rotating stress tensor diagram.

## Visualizer Engine Choice

This repository uses C# and WPF with native `Canvas` drawing primitives. For this application, WPF is preferable to a game or plotting engine because the core experience is interactive vector geometry: draggable points, editable labels, text boxes, menus, dialogs, screenshots, and Windows Help integration. WPF also keeps the app easy to package as a normal Windows desktop tool.

If this later grows into a cross-platform or browser-based tool, the best alternative would be TypeScript with SVG/Canvas in a desktop wrapper such as Tauri or Electron. For a Windows-first engineering app, C# and WPF are still the most direct fit.

## Features

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
- Settings, About, and README-based Help integration.

MCSTVisualizer 1.1:
- 3D mode:
  - Adds a "3D" tab next to the original 2D implementation
  - Displays an orthogonal 3D stress tensor diagram together with a "Mohr's Sphere" diagram
  - Adds `sigma_z`, `tau_yz`, `tau_zx` principal stress parameters
  - Adds `sigma1`, `sigma2`, and `sigma3` to the Mohr's Sphere 
  - Implements tri-axis rotation with three draggable rotation arcs around the 3D stress tensor
  - Diagram rotation can also be controlled by three sliders

## Build

# Developers:
Install the .NET Desktop SDK for Windows, then run these from the repo root:

```powershell
dotnet build .\MCSTVisualizer.sln [-c Release]
dotnet run --project .\src\MCSTVisualizer\MCSTVisualizer.csproj
```

# Users:
The ready-made executable can already be run from `src\MCSTVisualizer\bin\Release\net10.0-windows\win-x64`.

## Notes

- `Help -> Help` opens this README file.
- `Edit -> Settings`, `View -> Zoom`, and `View -> Screenshot` are included where requested. Screenshot export is implemented through the menu and the camera button.
- Saved CSV files can be reopened with `File -> Open`.

## Implemented Features

- Add stress invariants and optional strain transformation mode.
- Add a polar grid and snap-to-angle increments on Mohr's circle.
- Add units support with MPa/ksi/psi presets.

## Suggested Future Improvements

- Add a print-ready engineering report export.
- Add examples for common loading cases.
- Add automated tests for stress-transformation formulas.
- Make 3D rotation values textbox-editable.

