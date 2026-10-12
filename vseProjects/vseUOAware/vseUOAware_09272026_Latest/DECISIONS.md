# DECISIONS

Log of confirmed user decisions, corrections and constraints. Superseded entries are struck through, never deleted.

- 2026-10-11 16:23 **Constraint**: Terrain XML tool family (repair/create transitions, create terrain, edit Terrain.xml, edit Common.xml, guide) must support 3-way transitions (HashKeys with three distinct terrain IDs), not only 2-way A/B.
- 2026-10-11 16:24 **Constraint**: New custom terrains will have their own custom transition art; tools must accept user-supplied custom land/static art IDs per pattern, not rely only on copying an existing model pair.
- 2026-10-11 16:25 **Constraint**: Existing terrains missing transitions may get either custom transition art or pre-existing land art from art.mul; the repair tool must support both sources and let the user pick per terrain/pattern.
- 2026-10-11 16:31 **Constraint**: Priority: generated/repaired XML must be read by the MapCreator compiler and produce correct maps. A transition set may have as few as 1-2 pieces or the standard 12+ land IDs from art.mul, depending on the tile's use. Generated XML should include comments naming the tiledata texture used by each land tile. Accuracy over speed; no guessing.
- 2026-10-11 16:39 **Correction**: MapInfo.xml is out of scope (only sizes maps in the CreateFacetTemplate control). Altitude.xml may be editable later, low priority; not part of the transition tools.
- 2026-10-11 18:08 **OutOfScope**: Rebuilding MapCreator is the user's own future project in a new solution; do not start it, create folders for it, or propose a location in this solution. Terrain ID 4C (Death Forest) is considered dead; the Death Forest transition files are delete candidates, but nothing is deleted by the tools.
