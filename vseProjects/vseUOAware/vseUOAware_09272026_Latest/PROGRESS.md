# vseUOAware PROGRESS

## Current checkpoint (read this first)
- Chunks 1-3 of 8 DONE and tested: MapAnalysisTools (FindTileUsage, ConvertCoordinate), MapIntegrityTools (CheckMapIntegrity; facet 4 and UOP map0 0 bad), MapDiffTools (DiffMaps; edited vs UOP 256/256 differ, UOP vs UOP 0 differ). Untested: DiffMaps compareStatics, FindTileUsage with a real static ID. Plan sign-off: PENDING.
- Chunk 4 DONE and tested: Tools/MapStaticsTools.cs (FindStaticProblems, CheckWalkability). UOP map0 region: 3036 statics, 0 problems; facet 4 (600,600) 128x128: 12602 walkable, 3679 impassable, 103 static-blocked. DiffMaps compareStatics verified (1938 differing tiles in a 64x64 region). Defect-detection path of FindStaticProblems untested on a real defect.
- Chunk 5 DONE (file-level check only): Tools/MapExportTools.cs (ExportMapRegionPng). Facet 4 (500,500) 256x256 saved, 30785 bytes, bin\Debug\net10.0\uoaExport\Map\map_500_500_256x256.png. Image content not visually verified by the agent.
- Chunk 6 DONE (MapPlacementTools): facet 4 validate_coordinate_list 2 ok/3 problems (water, outside, impassable 0x0234); check_house_placement 7x7 at (600,600) blocked by 3 statics; 10x10 at (625,598) blocked by 24 tiles, Z spread 45. maxZSpread=5 is an assumed default.
- Chunk 7 DONE (SnippetTools): generate_code_snippet with 3 coords gave Point3D array plus Rectangle2D(600,598,31,3); rectangle 500,500,20x10 gave Rectangle2D plus center Point3D(510,505,0). run_build OK.
- Chunk 8 DONE: vseProjects.Testing/vseUOAware.Tests (xunit.v3), `dotnet run` in that folder = 11 passed, 0 failed (one test expectation was wrong and fixed). README/UPDATE/CREDITS/LICENSE(GPL-3.0) added. ALL 8 CHUNKS DONE. Note: editor edit tool once reported success without writing; verify on disk.
WorldMapExportTests.cs added (tests file; an empty leftover WorldMapExportToolsTests.cs should be deleted); `dotnet run` in test folder = Total 13, Failed 0 (verified from output file).
- Next action: none (plan sign-off still PENDING from user).
- Read only small slices of MapCoordinateReader.cs (GetRegion at line ~137) and MapTools.cs. Do not dump whole files or the ruleset.

## Done and verified
- Facet resolution, facetWidthTiles / facetHeightTiles, max 10,000 tiles, strict block-count checks.
- UOP padding: every map*LegacyMUL.uop holds exactly 1 block more than the flat mul (measured map0-5). CountBlocks subtracts 1 for UOP only (user-confirmed padding).
- Tested: facet 4, map0 UOP and map4 UOP reads, 7001 / 10008 / mismatched sizes rejected.
- Backup UOP folder: C:\Users\aasro\Desktop\uoAvox_09112026_Latest\Game\ultima (original, Z=-5 water). Working folder uoAvox is an edited map (Z=0).

## Plan (8 chunks, ~195 min estimated)
1. Region scanner, tile usage search, coordinate converter
2. Map integrity check
3. Map diff
4. Statics problem finder, walkability checker
5. Map to PNG export
6. Coordinate list validator, house placement check
7. Code snippet generator
8. Tests (vseProjects.Testing, outside solution), README / UPDATE / CREDITS

## Notes
- All new tools read-only. No new NuGet packages.
- Build needs the running vseUOAware server restarted first (exe lock).
- Address user as lowercase ghostbyte.

- OPEN (user decision, 10/10/2026): SixLabors ImageSharp license warning at build. Fix = apply for free Community license at licensing.sixlabors.com once the repo is public on GitHub, then add sixlabors.lic (Rule 17 if a key string). Warning is harmless meanwhile (build OK).
