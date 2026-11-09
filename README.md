# Visual Progression: Age Picker

A RimWorld 1.7 mod. Pick what your items look like and what they're called in each tech age, using the art of any mod
you have installed. Items made in that age get your pick and keep it, the way
[Visual Progression Framework](https://github.com/ColbyHepworth/VisualProgressionFramework) packs work.

Requires Harmony and Visual Progression Framework. Something also has to raise your colony's tech level, such as
Vanilla Factions Expanded - Tribals.

## Picking a look

1. Open an item's info card and click **Pick looks**.
2. Choose an age on the left.
3. Click a picture to use it in that age: the item's normal art, art other mods have for it, or any mod's art below
   that. Items with stack art, like meals, choose between whole folders, which change picture as the stack
   grows, and single pictures, which show at every stack size. Hover a folder to see when each picture is used.
4. Type a name to rename the item in that age. A name alone keeps the art the age already had. A look covers its age
   and the earlier ones down to the next look, so a Medieval look also shows in the Neolithic age.
5. Close the window and restart the game.

The mod settings list the items you've changed, to open again or reset.

Apparel, and items whose art has four directions, take names only.

## Sharing your picks as a pack

Click **Export as pack** in the mod settings, give the pack a name and an author, and click **Export**. It's written to
your Mods folder as a pack like any other Visual Progression pack, to keep, share or publish. After a restart it's in
the mod list.

The pack holds no art. Art from another mod stays in that mod, and the pack lists that mod as a requirement. A rename
that keeps another pack's look needs that pack too. A look with the item's normal art shows each player's own normal
art, retextures included. Looks for items from other mods are skipped when those mods aren't active. Exporting again
under the same name replaces the pack, and a name that's already another mod's folder is refused.

Your picks stay after an export, and they win over the pack, so nothing changes in game until you reset them.

## Status

Work in progress, not on the Workshop yet.

## How it works

Each pick becomes a look while the game loads its defs, the same kind of look a framework pack adds in XML. A pick
covers its age and the earlier ages down to the item's next look, and past the last look the item keeps its normal
art. When a pack already gives the item a look for that age, your pick replaces it.

Stack art takes its pictures' names from every mod's folder at that path. If the folder you pick names its pictures
differently from the item's own folder, the missing ones show the normal art.

## License

MIT
