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

## MapCreator tools scope (approved 10/2026) - Step 1 source-rule read DONE
- Terrain.xml: <Terrain Name ID TileID R G B Base Random>; ID = GroupID (byte). Altitude.xml: <Altitude Key Type Altitude R G B>; Key = palette index, Altitude = short Z.
- Altitude.cs uses 8bppIndexed bitmaps (LockBits) so source pixel INDEX = terrain GroupID / altitude Key.
- Transitions XML: <Trans><TransInfo Description HashKey(18 hex = 9 two-char GroupIDs, 3x3) [File]><MapTiles><MapTile TileID AltIDMod/></MapTiles><StaticTiles/></TransInfo>.
- MapInfo.xml: Map Name Num XSize YSize (Felucca 7168x4096, Ilshenar 2304x1600, Malas 2560x2048, Tokuno 1448x1448, TerMur 1280x4096).
- Not yet read: where compile reads Terrain.bmp (Transitions.cs matching/fallback). Next: read compile loop, then plan chunks for tools 2,1,5,6,12,10.
- Next action: locate compile loop (grep Terrain.bmp consumers in MapCreator.Engine), then write PLAN.md.

- CONFIRMED (Altitude.cs 425-475): Terrain.bmp is read via 8bppIndexed; pixel byte = terrain GroupID; Altitude.bmp = same size, byte = TerrianGroup(id).AltID. TransitionTable is a Hashtable keyed by HashKey string (duplicate key = error). No neighbourhood-lookup code found in Compiler/*.cs; still to locate (likely Map compile elsewhere).
- Next action: grep whole MapCreator tree for the lookup using i_Transitions/TransitionTable.Transition(, then write PLAN.md.

## Step 1 COMPLETE - compile rule (CompileYourNewMap.cs ~225-300)
- For each cell: key = 9 groupIDs as 2-digit UPPERCASE hex, order NW,N,NE,W,C,E,SW,S,SE; coordinates WRAP at edges (m-1 at 0 -> width, m+1 at width -> 0).
- Lookup exact key in transition Hashtable. Missing => NO transition: TileID = terrain.TileID (flat fallback, silent). Found => random MapTile (AltIDMod) else terrain TileID.
- GroupID 254 is forced to TileID 1078, Alt 0.
- Tool 2 = list distinct 3x3 keys present in Terrain.bmp (8bpp index) that have no XML rule and are not all-same-group (all-same fallback is normal). Count + sample coords.
- Next action: write PLAN.md (chunks: A tool2, B tool1, C tool5, D tool6, E pkg audit(12), F dup check(10)), then implement chunk A.

## Chunk A (tool 2 FindMissingTransitions) - written + built, NOT yet tested
- Files: vseUOAware Services\BmpIndexReader.cs, Tools\MissingTransitionTools.cs. Earlier interrupted command never ran (PowerShell parse error); files re-created with create_file.
- Build to temp folder: Build succeeded, 0 errors, 1 warning (known ImageSharp license).
- Test traceability: untested. Real fixture: MapCreator\Required\Development\Test Bitmaps\Map0\Terrain.bmp (29 MB, 7168x4096) + MapCreator.Engine\Compiler\XMLFacetData\Transitions.
- Next action: add automated test (small synthetic 8bpp BMP), run, then restart MCP for live check; then chunk B (tool 1).


## Chunk A TESTED
- Tests: vseProjects.Testing\vseUOAware.Tests\MissingTransitionTests.cs (4 tests: NoRules_ReportsAllNineNonUniformCells, RuleForCenterKey_IsNotReported, NonEightBitBmp_IsRejected, MissingFile_IsReported).
- Run via built exe (dotnet test fails: MTP/.NET 10 VSTest mode error, runner issue not code): Total 17, Failed 0.
- Untested: real 29 MB Map0 Terrain.bmp, live MCP call (needs restart).
- Next action: restart MCP, live-run tool 2 on Map0 fixture, then chunk B (tool 1 source bitmap checker).

## Chunk A1 VERIFIED - ignoreGroupIds
- Domain rule (ghostbyte): black/Blank (GroupID 19) is an intentional placeholder/buffer; it only really meets rock. Map0 black strip is official, not a defect.
- Program.cs needed .WithTools<MissingTransitionTools>() (earlier live null calls). Live Map0 earlier: 324 distinct missing patterns, four 4,096-cell Blank/Deep Water strips.
- Code: FindMissingTransitions(..., maxResults=30, ignoreGroupIds="19"); skips non-uniform cells with an ignored group and at most one other group. Empty string reports all. Bad value returns error.
- Tests: new IgnoreGroupTests.cs (3 tests). create_file/replace reported success but wrote nothing; file written via PowerShell and read back (1,998 bytes).
- Evidence: exe run Total 20, Failed 0 (was 17).
- Untested: live Map0 with default 19 (needs server restart).
- Next action: restart MCP, run tool 2 on Map0 (A2), then docs/violation log (A3), then chunk B.

## Chunk A2 - Map0 run (reflection route)
Two earlier routes failed (PowerShell reflection var error; ANSI parse error). Third route: temp console project + reflection. Result: 58,733 non-uniform cells, 16,384 skipped as ignored Blank edges, 320 distinct missing patterns, 10,738 cells hit.
Root cause of tool not visible in MCP: class lacked [McpServerToolType]; fixed, build OK.
Live MCP call still PENDING after restart (tool not in client tool list).

## Chunk A2 VERIFIED (live MCP)
After adding [McpServerToolType] and restart, mcp_vseUOAware_find_missing_transitions ran on Map0 (maxResults 8, default ignore 19): 58,733 non-uniform cells, 16,384 skipped as ignored Blank edges, 320 distinct missing, 10,738 cells hit. Matches reflection run.
Next: A3 docs (README/UPDATE/CREDITS), Rule 22 analyzer check, then chunk B (tool 1).

## Chunk A3 VERIFIED
UPDATE.md, README.md, PLAN.md synced; analyzers enabled in csproj (EnableNETAnalyzers, latest-recommended, EnforceCodeStyleInBuild); TreatWarningsAsErrors left off pending user choice (Rule 22E).
Pre-existing analyzer warnings (out of scope, Rule 22D): CA1305 (AnimationTools, AnimDataTools), CA1822 (many Tools), CA1859 TileDataReader.DescribeFlags, CA1861 MobTypesReader.
Build note: running MCP server locks bin; build to temp output with -o.
Next action: Chunk B tool 1.

## Chunk B - tool 1 (check_source_bitmaps)
- An earlier here-string write was canceled; file_search also shows stale results. Rewrote Tools\SourceBitmapTools.cs with create_file, then confirmed on disk: 3765 bytes, 69 lines.
- A replace_string_in_file edit to Program.cs reported success but changed nothing; reapplied via PowerShell and confirmed line 38 .WithTools<SourceBitmapTools>().
- Build OK (0 errors), CA1822 fixed by making the method static. Tests: 27 total, 0 failed (7 new in SourceBitmapTests.cs).
- Live MCP check pending a restart of the vseUOAware server. README.md and UPDATE.md still to update.
- Next action: restart MCP, live-call check_source_bitmaps on the official Map0 files, then update README.md, UPDATE.md and PLAN.md.

- Chunk B: official Map0 run OK (7168x4096, all indices in Terrain.xml); 28 tests pass. README/UPDATE synced. Live MCP exposure needs restart. Next action: tool 5.

- Chunk B live MCP check after restart: check_source_bitmaps on official Map0 returned 'Terrain.bmp 7168x4096. All Terrain.bmp palette indices exist in Terrain.xml.' Chunk B fully verified. Next action: Chunk C tool 5 - spec needed from user.

XML tool family plan approved (go). Next action: D1 recon. Nothing coded yet.

D1 recon findings (verified in source: CompileYourNewMap.cs 222-340, Transitions.cs 500-600, 955-1010):
- Compiler builds a 9-ID key (NW,N,NE,W,C,E,SW,S,SE) per cell and does an EXACT hashtable lookup. No wildcards. A 3-way transition is just a key with 3 distinct IDs.
- Key missing: cell gets the plain Terrain.xml TileID and no statics. Duplicate keys: Load skips the later one silently (first wins).
- Tile choice: MapTileCollection.RandomTile = round(Rnd()*(Count-1)); with rounding, first and last entries are picked about half as often as middle ones. Static choice uses same pattern.
- Group 254 is hardcoded to tile 1078. Any exception in the lookup loop aborts the compile with a log line (Location/Hkey).
- Open: empty MapTiles behaviour (GetRandomMapTile lines 104-111 not fully read), MassLoad file order, Altitude.xml/MapInfo.xml, 3-way file survey.
Next action: finish D1 open items.

D1 recon complete (verified):
- Empty MapTiles: GetRandomMapTile returns null -> compiler falls back to the plain Terrain.xml TileID (+ terrain Base altitude). Statics still applied.
- Load order: MassLoad walks FacetData.EnumerateXml(Transitions, recurse) = SortedSet, OrdinalIgnoreCase, by relative path. First file in that sort order wins a duplicate key. Custom file with same relative path replaces the original (merged into one entry).
- Survey: 47782 keys total in shipped Transitions; 5385 have 3+ distinct IDs (e.g. Cobblestones-DarkWoodenFloor-Grass file, key 010101181919181919). 3-way keys live in files named with three terrains.
- Altitude.xml = <Altitude Key Type Altitude R G B>; MapInfo.xml = <Map Name Num XSize YSize> (not transition related).
Next action: D2 shared XML helper library (needs design approval for file/class names).

## D2 shared XML helper (Services\MapCreatorXml.cs)
- Built clean, 0 errors, no warnings in the file. Tests: 32 total, 0 failed (4 new in MapCreatorXmlTests.cs). User ran the test exe and pasted the output.
- Real corpus: 47783 entries, 0 duplicates. D1 survey said 47782 - 1 off, UNRESOLVED, check before D3.
- Not yet covered: Validate, ValidateTerrains, WriteTransitionFile round-trip, texture comments.
- Next: resolve the count difference, add writer/validator tests, then D3.

## D2 COMPLETE
- Tests: 38 total, 0 failed. MapCreatorXmlTests.cs has 10 tests: ParseTileId, keys (2-way/3-way), Validate (ok/bad key/unknown terrain/ranges), ValidateTerrains (dup ID/colour/254/range), FindDuplicates (first wins, case-insensitive), writer round-trip with comments and empty MapTiles, real Terrain.xml, real Transitions.
- Count resolved: raw XML has 47783 TransInfo/HashKey; loader matches. D1's 47782 was a survey off-by-one.
- Texture comments: writer takes a commentFor delegate (tested). The tiledata-backed provider is NOT built yet - needed by D3/D5, build it there.
- Next: D3 RepairTransitionXml.

## D3 design (user chose repair level 2, output = Custom mirror + INSTALL.md)
- Tool: RepairTransitionXml(transitionsFolder, terrainXmlPath, outputFolder, [tiledata], [fills]).
- Fixes only mechanical faults it can prove: drops entries with bad key format / unknown terrain ID / out-of-range tiles (reported, never silent), removes later duplicate keys the compiler already ignores.
- Fills missing keys ONLY from tile IDs the user supplies in fills. Never auto-picks art.
- Writes changed files to outputFolder\XMLFacetData\Custom\Transitions\ plus INSTALL.md + REPORT.md. Originals untouched.
- Texture comment = land name + TextureId from TileDataReader.GetLandDetail; no tiledata -> no comment.
- Status: design only, no code yet.

## D3 complete (service + MCP tool; live MCP check pending restart in D8)
- Services\TransitionRepair.cs, Tools\TransitionRepairTools.cs (RepairTransitionXml), registered in Program.cs.
- Tests: 8 new TransitionRepairTests; full suite Total 46, Failed 0. Main build 0 errors (CA1822/CA1861 warnings only).
- Next: D4 Terrain.xml tools.

## D4 complete (service + MCP tool; live check after restart)
- Services\TerrainEdit.cs, Tools\TerrainEditTools.cs (CreateTerrainType), registered in Program.cs.
- Tests: 7 new TerrainEditTests; suite Total 53, Failed 0.
- Next: D5 CreateTransitionXml.

## D3/D4 live MCP verification (ghostbyte session)
- Direct stdio tools/list returned 74 tools incl. RepairTransitionXml and CreateTerrainType; client needed a new session to see them.
- LIVE CreateTerrainType: OK, new ID 59, wrote Custom\Terrain.xml + INSTALL.md to temp, originals untouched.
- LIVE RepairTransitionXml: OK, scanned 47783 entries, dropped 0, trimmed 0, filled 0, 407 unknown-terrain REPORT-ONLY (RedTile, Lava 3way, Cave keys; ids 01/0F/F0/F1/0D/12/4A/4C absent from Terrain.xml). Originals untouched.
- Next: D5 CreateTransitionXml.

## D5 CreateTransitionXml - code done
- Added Services/TransitionCreate.cs, Tools/TransitionCreateTools.cs, registered in Program.cs, 3 tests; exe run: 56 total, 0 failed. Live MCP check pending (needs new session). Next: D6 Common.xml.
- Unknown IDs: HashKey values are Terrain.xml group IDs (hex). Truly missing from Terrain.xml: 4C(76), F0(240), F1(241). 01,0D,0F,12,4A exist (Grass 3, Lava 500, Red Tile 512, Rock 556, Dungeon Cave w/o Static 581).

## AuditTransitionXml (user-requested all-in-one audit)
- Added Services/TransitionAudit.cs, Tools/TransitionAuditTools.cs, registered, 2 tests; exe run 58 total, 0 failed. Read-only, writes AUDIT.md to new folder. Live MCP check pending (new session). User decision: Death Forest (ID 4C) is dead; build tools so users handle their own copies.

## D6 EditCommonXml, D7 guide, D8 live check - DONE
- Added Services/CommonEdit.cs, Tools/CommonEditTools.cs (3 tests), Tools/MapCreatorXmlGuideTools.cs (1 test), registered. Test exe: 62 total, 0 failed.
- Live stdio: tools/list = 78 tools incl. audit_transition_xml, edit_common_xml, create_transition_xml, get_map_creator_xml_guide. Live audit on real XMLFacetData: 356 files, 47783 rules, KEEP 352, REDO 1, DELETE-CANDIDATE 3; missing IDs 4C(156 rules), F0(209), F1(251); no duplicates. AUDIT.md written, originals untouched.
- Live calls of create_transition_xml / edit_common_xml with real data not yet run (unit-tested only).

## Live MCP verification after restart - DONE
- create_transition_xml live: OK, wrote Live Test.xml (290 bytes, read back, key 0000000000010101FF, tiles 500/501). edit_common_xml live: add on Lava correctly refused (exists); replace OK, Common.xml 77 rules, 26373 bytes read back. Originals untouched. All D5-D8 tools now live-verified.

## Source analysis tools (5)
- Added map_program_pipeline, extract_input_contract, find_hardcoded_values, analyze_change_impact, compare_compile_outputs (all read-only).
- Tests: 67 total, 0 failed (5 new).
- NOT yet done: live MCP run on the real MapCreator source after server restart; guide text update.
- Live run on real MapCreator source done (83 tools load). Guide text now lists the 5 source tools.

## create_transition_template (84th tool)
- Added Services/TransitionTemplate.cs and the tool in Tools/SourceAnalysisTools.cs. Writes 12 fill-in-the-blank rules (REPLACE_ME with LAND TILE HERE / STATIC TILE HERE direction comments) into a new empty folder.
- Live run: Grass (1) + Red Brick NS (34) wrote 12 rules, read back OK. Server loaded 84 of 84 tools.
- Tests: TransitionTemplateTests (2 new). Total 69 passed, 0 failed.
- Open: Grass-to-Red-Brick still needs real tile IDs; F0/F1 meaning in RedTile-Grass.xml is unproven.
