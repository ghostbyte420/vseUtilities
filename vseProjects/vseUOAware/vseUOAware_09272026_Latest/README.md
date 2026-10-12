## vseUOAware
</br>

---
vseUOAware is a read-only MCP server that lets an AI assistant inspect Ultima Online client and map data: maps (flat MUL and UOP, including custom sizes divisible by 8 up to 10,000 x 10,000 tiles), statics, tiledata, art, animations, sounds, gumps and more.

Map tools: coordinate inspection, map diff, map integrity check, tile usage search, static problem finder, walkability check, coordinate list validator, house placement check, coordinate converter, region to PNG export, and a C# snippet generator (Point3D / Rectangle2D).

Use it to check a custom map, find mistakes before a shard sees them, and generate code from real map data. It never writes to your UO data files.

---

Licensed under GPL-3.0 (see LICENSE.md).

MapCreator checks: find_missing_transitions lists Terrain.bmp 3x3 patterns that have no Transitions XML rule (official Blank group 19 ignored by default).

- check_source_bitmaps: checks Terrain.bmp (8-bit indexed, size divisible by 8, optional Altitude.bmp size match) and that every palette index exists in Terrain.xml.
