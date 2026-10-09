# Liquify for Paint.NET

Liquify is a Paint.NET effect plugin that distorts an image as if it were made of liquid. You push, twist, swell and pinch the image with a brush, and can freeze areas to protect them.

It needs Paint.NET 5.

## Install

1. Close Paint.NET.
2. Copy `Liquify.dll` into `Documents\paint.net App Files\Effects\`.
3. Start Paint.NET. The effect is under **Effects > Tools > Liquify**.

<!-- The plugin's Help link and F1 open this page at #using-it (ShowHelp in ConfigDialog.cs).
     Renaming this heading changes the anchor and breaks them. -->
## Using it

Liquify works on the current layer. Pick a tool on the left, set the brush on the toolbar, and drag on the image. Nothing is applied to the layer until you press OK.

### Tools

| Tool | Key | What it does |
|---|---|---|
| Push | P | Drag to push the image along |
| Twist left | L | Hold or drag to twist the image |
| Twist right | R | Hold or drag to twist the other way |
| Bloat | B | Hold or drag to swell the image |
| Pucker | S | Hold or drag to pinch the image |
| Reconstruct | E | Hold or drag to undo the distortion gradually |
| Freeze | F | Paint over areas to protect them from the other tools |
| Thaw | T | Paint over frozen areas to unprotect them |

The buttons under the tools reset all distortion, thaw everything, invert the frozen area, and load or save a mesh.

### Brush

- **Size** is the brush diameter in pixels. `[` and `]` change it by 1, and `Ctrl+[` and `Ctrl+]` by 5.
- **Pressure** is how strongly the brush acts.
- **Pen tablets**: pressing harder makes the brush act more strongly, up to the Pressure setting. The eraser end undoes the distortion (Reconstruct), or thaws when the Freeze tool is selected.
- **Density** is how much of the brush acts at full strength. Low fades out from the center; high is even to the edge. The fainter inner ring on the brush shows where it is down to half strength.

### Viewing

| Action | How |
|---|---|
| Zoom | `Ctrl` + wheel, `Ctrl +`, `Ctrl -`, or the zoom box |
| Zoom to 100% / fit | `Ctrl+0` / `Ctrl+B` |
| Scroll | Wheel, `Shift` + wheel for sideways, or two-finger scrolling |
| Pan | Hold `Space` and drag, or drag with the middle button |
| See the original | Hold `O`, or hold the **Original** button |
| See the whole document | Hold `A`, or hold the **All layers** button |
| Undo / redo | `Ctrl+Z` / `Ctrl+Y` or `Ctrl+Shift+Z` |
| Open this page | `F1`, or the **Help** link at the bottom of the window |

The **View** menu controls what the canvas shows besides the image:

- **Frozen areas** (`M`) tints frozen areas red.
- **Mesh grid** draws a fine or coarse grid that bends with the image.
- **Layers above** shows the visible layers above the current one, with their blend modes.
- **Canvas background** chooses what shows through the layer's transparent parts: a checkerboard, a color, an image from the clipboard, the layer beneath, or all layers beneath.
- **Around the canvas** sets the color of the area around the image.

The background choices are also on the canvas's right-click menu. The brush settings, the grid, the backgrounds, the layers-above setting and the window's size and position are remembered the next time you open Liquify, until Paint.NET is closed.

### Mesh files

**Save mesh** and **Load mesh** write and read the distortion as a `.msh` file, so it can be applied again or to another image. The format is the one Photoshop's Liquify uses, so meshes can be exchanged with it. Frozen areas are not part of the file.

### Updates

When you open Liquify, it fetches a small text file from the latest [pyrochild plugin pack](https://github.com/bsneeze/pdn-pyrochild-plugin-pack/releases/latest) on GitHub to see whether that has a newer version. It does this only the first time Liquify is opened after Paint.NET starts, and not at all if Liquify isn't opened. If it has, a banner above the image says so, with links to see what's new and to get it. **Not now** hides the banner for three days; the ✕ closes it until Liquify is next opened. A beta build also looks at this project's releases for a newer beta. Untick **Check for updates** at the bottom of the window to turn it off.

## Building

You need the .NET 9 SDK and Paint.NET 5 installed.

```powershell
dotnet build Liquify.sln -c Release
```

- The project references Paint.NET's assemblies under `C:\Program Files\Paint.NET\`. If it is installed elsewhere, add `-p:PdnDir=<folder>\`.
- The build copies `Liquify.dll` into your Paint.NET effects folder. Close Paint.NET first, or the copy fails with a warning.
- A Debug build names the effect "Liquify DEBUG", so it can be told apart from a Release build in the menu. It isn't optimized, so the brush and the canvas are several times slower than in a Release build.

Run the tests with:

```powershell
dotnet test Liquify.sln
```

They cover the mesh maths, the `.msh` file layout, undo history, the brush and the canvas control. They don't cover the dialog itself, so changes to it need trying in Paint.NET.

## License

MIT. See [LICENSE](LICENSE).
