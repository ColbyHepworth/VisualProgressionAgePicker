# Visual Progression: Look Picker

A RimWorld 1.7 mod. Pick what your items look like and what they're called in each tech age, using the art of any mod
you have installed. Items made in that age get your pick and keep it, the way
[Visual Progression Framework](https://github.com/ColbyHepworth/VisualProgressionFramework) packs work.

Requires Harmony and Visual Progression Framework. Something also has to raise your colony's tech level, such as
Vanilla Factions Expanded - Tribals.

## Status

Work in progress. Picks are read from the mod's settings file and take effect after a restart. The window for picking
them in game is coming next.

## How it works

Each pick becomes a look while the game loads its defs, the same kind of look a framework pack adds in XML. A pick
covers its age and the earlier ages down to the item's next look, and past the last look the item keeps its normal
art. When a pack already gives the item a look for that age, your pick replaces it.

## License

MIT
