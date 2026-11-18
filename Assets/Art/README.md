# Game art

Every picture in the game comes from this folder. All of it is **placeholder art** cut from one low-resolution
concept sheet (issue #41), so each file is small (about 80 to 470 px). Replace it with final art when it is ready.

## How to swap a picture

1. Make the new picture as a transparent PNG (backgrounds can be opaque).
2. Save it over the old file **with the same name** in the same folder.
3. Unity re-imports it. Every item, icon and scene object that used the old picture now shows the new one.

PNGs added here are imported as UI sprites automatically (`GameArtImporter`). If you add a new file or change which
file is used where, update the tables at the top of `Assets/Scripts/Editor/GameArt.cs`, then run
**Witch > Build MainLab Test Scene**.

## Files and where they are used

| File | Used for | Final size |
| --- | --- | --- |
| `Kitchen/BowlOfLemons.png` | Lemon on the counter | 512 x 512 |
| `Kitchen/SqueezedLemon.png` | The lemon after squeezing | 512 x 512 |
| `Kitchen/Lemon.png` | Citric Acid icon (HUD and book) | 512 x 512 |
| `Kitchen/EmptyGlass.png` | The glass while empty | 512 x 512 |
| `Kitchen/LemonJuiceGlass.png` | The glass once filled with juice | 512 x 512 |
| `Kitchen/SaltJar.png` | Salt on the shelf, Table Salt icon | 512 x 512 |
| `Kitchen/BakingSodaBox.png` | Baking soda on the shelf, Baking Soda icon | 512 x 512 |
| `Kitchen/FlourSack.png` | Flour on the shelf, Flour icon | 512 x 512 |
| `Kitchen/WaterJug.png` | Water jug on the counter | 512 x 512 |
| `Kitchen/WashingSodaBowl.png` | Washing Soda icon (made by heating baking soda) | 512 x 512 |
| `Kitchen/WallShelf.png` | The wall shelf plank | 2048 wide |
| `Kitchen/StoveTop.png` | The stove | 1024 wide |
| `Kitchen/KnobOff.png`, `Kitchen/KnobOn.png` | Heat knob, switches with the heat | 256 x 256 |
| `Kitchen/PotEmpty.png` | The pot while empty | 1024 wide |
| `Kitchen/PotWithLiquid.png` | The pot with something in it, Salt Water icon | 1024 wide |
| `Kitchen/PotFizzing.png` | Pot for a few seconds after a reaction, Fizzy Bubbles icon | 1024 wide |
| `Backgrounds/KitchenBackground.png` | Kitchen backdrop (fills the screen, sides cropped) | 2048 x 3072 |
| `Backgrounds/GardenBackground.png` | Garden backdrop | 2048 x 3072 |
| `Garden/LemonTree.png`, `RockSalt.png`, `Nahcolite.png`, `Chamomile.png` | Lore pages in the book and the garden | 512 x 512 |
| `UI/BookPage.png` | Book page (9-sliced, so corners stay sharp when it stretches) | 1024 wide |
| `UI/BookButton.png`, `UI/GardenButton.png`, `UI/HintButton.png` | HUD buttons (the words are part of the picture) | 512 wide |
| `Characters/WitchHappy.png` | Medea idle and cheering after a reaction | 1024 tall |
| `Characters/WitchThinking.png` | Medea after a wrong try or a hint | 1024 tall |

The backdrops are landscape for now. Final backdrops should be portrait (2048 x 3072) so nothing is cropped.
