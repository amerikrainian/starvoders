# Changelog

## V0.1.7

- On AOE cards, we announce target entities first as opposed to coordinates.

## V0.1.6

- Some tiles fell back to their internal names because the game had no player-facing names for them. This has been fixed.
- Some fixes for endgame boss fights. All I'm gonna say for that.
- We now display when an artifact is marked as used.
- We now tell you when a spell's cooldown has elapsed.
- We also tell you when cards get added to your pile(s).
- We now pick up the secret quest panel. Good luck finding where though or when the hell that thing shows up.
- When accepting the secret quest, you can now read the artifacts it adds to your deck. Apparently those were special, go figure.
- In the compendium album, it's now possible to read pilot stats and memories (they had poor labels before).
- Update and label some unique boss counters.
- When cuing up multiple achievements we now keep the dialogue up so as to avoid spamming you with landing on an item under the popup after dismissing one.
- In the compendium, search field should no longer try and grab your focus every once in a while when just tabbing through.
- Fixed reading compendium values when they aren't fully settled: no more "??% found"
- We now distinguish between entities leaving the board, being destroyed, dissipating, what have you.
- Fixed tile changes sometimes being dropped.

## V0.1.5

- Permanent modifiers for encounters should no longer randomly be inserted into seemingly arbitrary points in focus speech.
- Cards that push an entity left or right, like Pusher, now offer the directions around the entity you picked, not around yourself or your haunted Summon.
- If the entity is against the edge, pushing it into the wall is now possible too.
- The Keeper's spells now list their empty slots after the spells, both in fights and in the deck viewer.

## V0.1.4

- Replaying cards for which you cannot meet the conditions now offers the game's skip repeats button. You can also press escape as it will now have the same effect.
- When playing jabs, we should no longer randomly lose the draw option.
- Enemies now report when they're immune when scrolling over them.
- Per request, entities with shock have their own category.
- Per request, you can now rebind mod keys, keeping in mind some actions are game-specific and must be done in options as opposed to mod settings.
- Haunting should properly read now.
- Booby traps are now labeled as such.

## V0.1.3

- Card/artifact rarities now show in buffers.
- Fixed typeahead search being a bit too eager on the press any button to play screen.

## V0.1.2

- Fixed special encounters like Holes! and Cloudy reading a placeholder title instead of their name. Their tile rule (e.g., what a Hole does) is now in the encounter's details.
- Ctrl+D (Doom) now works anywhere in a run, not just in fights. Ctrl+S reads your Stars the same way.
- I, J, K and L now move like the arrows in fights, and U, O, N and M move diagonally on the board.
- Cells now read what's on them before their coordinates, and the coordinates lose their comma.
- Combo gained after you end your turn, e.g., from rockets, is now announced.

## V0.1.1

- Fixed the unlock button detection breaking after I refactored the interface to be more condenced. My bad.

## V0.1.0

- We now allow you to rebind in-game actions.
- Compress the run into one tab stop.
- Cards, artifacts, and rewards now get rendered as horizontal rows.
- Fixed invader cards not showing descriptions on an encounter screen.
- Your hand now gets rendered below the grid. Since it might be a little more annoying to go to it now, alt+h jumps to it.
- When purging, we no longer spawn a separate list of cards and reuse your hand. Whoops?
- Added ability to search the compendium.
- You can now input game seeds upon unlocking the ability to do so.

## V0.0.9

- Add some more useful buffers: player, enemies, hand.
- Shift and digits now announce the corresponding card you would play when pressing that number.

## V0.0.8

- Player's summons like puppets now have a category.
- Fixed the mod repeating encounter objective when abandoning a run.
- Allow the game's keys to go through: A for draw, S for discard, R for rewind, F for more info (same as backspace).

## V0.0.7

- Added support for other characters and their resources, e.g., mana/heat.
- Added support for challenges.
- In the daily run, the deck is now in the same tab stop with everything else.

## V0.0.6

- Improve card readouts.

## V0.0.5

- Makes Min's unlock screen accessible.
- Cards that shift their AOE will now report the new area as you move your cursor.

## V0.0.4

- Sandbox settings are accessible now. Pack and boss rows read banned or allowed, and locked ones just say locked.
- Ctrl+B in a fight reads how many bombs you have out of the limit.
- The bomb count is no longer announced every time you move over a bomb or a target tile. it is still spoken once during previews.
- Things destroyed together are now grouped.
- Switching the language from the main menu now switches the mod's own words right away, instead of reading the combo box back in the old language.
- Fix home/end not behaving as they should on the grid; added page up/down for symmetry.
- Shift and arrows skip the labels on the grid, e.g., doom rows, spawn rows. Hopefully they'll be more useful now.

## V0.0.3

- Game-ending scene dialogues are accessible now.
- First pass at reading out achievements.
- End screen should no longer spam you with the amount of xp you received, as you don't start on it as soon as the screen shows up.
- You can now see what you've unlocked!
- It should indeed be possible to start new runs now, whoops?
- Added accessibility to the pack master.

## V0.0.2

- Add more screen coverage.

## V0.0.1

- First release