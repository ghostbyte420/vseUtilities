### Changelog:  ```10/11/2026```
- Added check_source_bitmaps (SourceBitmapTools): 8-bit BMP check, size divisible by 8, Altitude.bmp size match, Terrain.xml palette-index check.
- Registered in Program.cs; 8 new tests (28 total passing); official Map0 verified: 7168x4096, all indices known.
\n### Changelog:  ```10/11/2026```
---
- find_missing_transitions: new tool; reports Terrain.bmp 3x3 patterns with no Transitions XML rule. Default ignoreGroupIds=19 skips official Blank edges (empty string reports all).
- Fixed missing [McpServerToolType] so the tool is exposed over MCP.
- Enabled Roslyn analyzers (EnableNETAnalyzers, latest-recommended, EnforceCodeStyleInBuild).

### 📋Changelog:  ```10/11/2026```
---
- Added export_client_world_map: reproduces the uoAvox client world map image (radarcol, drawable statics, hued statics, Z shading, 2px padding).

### 📋Changelog:  ```10/10/2026```
---
- Added map tools: find_tile_usage, convert_coordinate, check_map_integrity, diff_maps (with optional statics compare).
- Added find_static_problems and check_walkability.
- Added export_map_region_png (radarcol colours, max 1024x1024).
- Added validate_coordinate_list and check_house_placement.
- Added generate_code_snippet (Point3D array, Rectangle2D, centre point).
- Added unit tests in vseProjects.Testing/vseUOAware.Tests (outside the solution).
- Added README.md, UPDATE.md, CREDITS.md and LICENSE.md (GPL-3.0).

