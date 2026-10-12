# Plan: vseUOAware MapCreator helper tools (1,2,5,6,12,10)

## Status
Last updated: 2026-10-11 (local time)
Checkpoint: A1-A3 verified (live Map0 run, docs synced); B verified (tool 1, 28 tests, Map0 run; live MCP needs restart); next = Chunk C (tool 5). See PROGRESS.md.

Legend: [ ] Not started; [>] In progress; [x] Completed and verified; [!] Failed; [||] Paused.

## Objective
Add read-only MCP tools that check MapCreator inputs (Terrain.bmp, XML rules) using MapCreator real rules.

## Scope
- In scope: vseUOAware Tools/Services, vseUOAware.Tests, PROGRESS/PLAN/README/UPDATE/CREDITS docs.
- Out of scope: any change to MapCreator source or input assets.

## Assumptions
- Blank (GroupID 19) is an intentional placeholder/buffer; ignored by default in tool 2, empty ignore list reports all.
- Tests run via built exe (dotnet test has the .NET 10 MTP issue).

## Chunk Contracts and Visible Checklist
| ID | Status | Severity | Deliverable | Estimate | Verification | Timeout |
|---|---|---|---|---|---|---|
| A1 | [x] | S3 - Rules touched: 8, 12 | Tool 2 FindMissingTransitions + ignoreGroupIds (default 19) | done | 20 tests, 0 failed (exe) | N/A |
| A2 | [x] | S3 - Rules touched: 8, 25 | Restart live server, run tool 2 on Map0, confirm 4,096-cell strips excluded | 5 min | live call output | N/A |
| A3 | [x] | S3 - Rules touched: 8, 25F | Docs sync: PROGRESS, README, UPDATE, CREDITS; violation log entry | 10 min | read-back of each file | N/A |
| B | [x] | S3 - Rules touched: 8, 12 | Tool 1 source bitmap checker | 15 min | unit tests + build | N/A |
| C | [ ] | S3 - Rules touched: 8, 12 | Tool 5 | 15 min | unit tests + build | N/A |
| D | [ ] | S3 - Rules touched: 8, 12 | Tool 6 | 15 min | unit tests + build | N/A |
| E | [ ] | S3 - Rules touched: 8, 12 | Tool 12 quick check | 10 min | unit tests + build | N/A |
| F | [ ] | S3 - Rules touched: 8, 12 | Tool 10 quick check | 10 min | unit tests + build | N/A |

## Timing and Execution Policy
- Measured elapsed recorded per chunk in PROGRESS.md; estimates are not guarantees.
- Retry budget: one identical write route max; verify every write by read-back (create_file proved unreliable).

## Risks / Open Questions
- Tool 2 default hides Blank plus one other group; optional future warning for Blank next to non-rock.

## Approval
- Written by: GitHub Copilot
- Execution approved by: ghostbyte (scope: tools 2,1,5,6,12,10)
- Reviewed By / Approved By: PENDING

## Handoff / Scope History
- Exact next action: Chunk C tool 5 (confirm its spec first).
- User action required: No (restart MCP only after new tools are built).

## XML Tool Family (approved by ghostbyte: go)
Constraints: 3-way transitions; custom art IDs and art.mul candidates; texture comments from tiledata; output to Custom mirror + INSTALL.md; no guessing.
Install fact (verified FacetData.cs): XML is embedded; files under Compiler\XMLFacetData\Custom\ override originals; rebuild MapCreator.Engine.
| Chunk | Status | Deliverable |
| D1 | [ ] | Recon: tile-choice (MapTiles.RandomTile), 3-way key matching, Altitude/MapInfo xml, 3-way survey |
| D2 | [ ] | Shared XML helper library |
| D3 | [x] | RepairTransitionXml |
| D4 | [x] | EditTerrainXml + CreateTerrainType |
| D5 | [ ] | CreateTransitionXml |
| D6 | [ ] | EditCommonXml |
| D7 | [ ] | GetXmlEditGuide |
| D8 | [ ] | Docs + live MCP check |

D2 [x] shared XML helper done, 38 tests pass (see PROGRESS.md).

- [x] D5 CreateTransitionXml  [x] D6 EditCommonXml  [x] D7 guide  [x] D8 live verification (audit live; create/edit live-call still to do)
