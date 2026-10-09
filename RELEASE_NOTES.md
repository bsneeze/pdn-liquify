# Liquify release notes

## Next release

### New

- **Pen tablets.** Pen pressure controls the brush's strength, up to the Pressure setting. The eraser end undoes the distortion, or thaws when the Freeze tool is selected.
- **Tool shortcuts.** P, L, R, B, S, E, F and T pick the tools, and a status line says what the current tool does.
- **Before and after.** Hold O, or the Original button, to see the untouched image.
- **See it in the document.** Hold A, or the All layers button, to see every visible layer with your changes. View > Layers above keeps the layers above on screen as you work.
- **Layer backgrounds.** The canvas can show the layer beneath, or all layers beneath, through the layer's transparent parts.
- **Mesh grid.** A visualization of the distortion mesh. Fine or coarse grid that bends with the image.
- **More mask tools.** Thaw everything, invert the frozen area, and hide the red tint (M).
- **Reset all** clears the distortion and keeps the frozen areas.
- **Panning and zooming.** Space-drag or middle-drag to pan. Ctrl+wheel, Ctrl +, Ctrl -, Ctrl+0 and Ctrl+B (fit) to zoom. Two-finger touchpad scrolling works in both directions.
- **Zoom to fit.** The dialog opens with the image fitted to the window, and "Fit" in the zoom box keeps it fitted.
- **A View menu** holds the tint, grid, layer and background choices.
- **Brush guides.** An inner ring shows where the brush is down to half strength, and changing the size or density shows the brush on the canvas for a moment.
- **Live color preview** when picking a background color.
- **Remembered settings.** The grid, the backgrounds, the layers-above setting and the window's size and position are remembered along with the brush, until Paint.NET is closed.
- **A prompt before discarding** changes on Cancel, Esc or the close box.
- **Help.** F1, or the Help link at the bottom of the window, opens the instructions.
- **Update check.** A banner above the image says when the plugin pack has a newer version. Liquify finds out by downloading a small file from GitHub when it opens, the first time after Paint.NET starts. The Check for updates box at the bottom of the window turns it off.

### Better

- **Pen strokes start at once.** Liquify no longer waits to see whether a pen touch is a press-and-hold.
- **Sharper results.** Final render uses supersampling where the distortion squeezes the image together for a cleaner result with less jaggies.
- **Faster.** Strokes and the preview use all processor cores, and scrolling and zooming stay smooth on large windows and 4K screens.
- **No pause after a large stroke.** Saving a stroke for undo no longer freezes the window, and takes a fraction of the time and disk space.
- **Less memory.** The distortion mesh takes a quarter less, undo is kept compressed on disk, and Liquify no longer copies the layer you are editing.
- **Undo keeps frozen areas,** and loading a mesh can be undone.
- **Safe when memory or disk space runs out.** A stroke, undo or redo that can't be completed is stopped or taken back, with a message saying what ran out. Your earlier work is kept.
- **An error during a stroke** shows a message and the stroke can be undone.
- **Exact brush sizes.** A brush covers as many pixels across as its size. It used to cover one fewer, and odd sizes matched the even size below.
- **No leftover temporary files,** even if Paint.NET crashes.
- **The background menu opens quickly** with a large image on the clipboard.
- **Dark mode throughout,** with no flash of light controls on opening.
- **Tooltips** give the shortcut keys and explain Pressure and Density.
- **Mesh files** are labelled as Photoshop compatible in the file dialogs.

### Fixed

- With a pen, a stroke started straight away on an image large enough to scroll moved the view instead of drawing.
- Hangs and crashes with very small or invalid brush sizes.
- A corrupted mesh on very large images and on images one pixel wide.
- A hang when closing the dialog in the middle of a stroke.
- A stroke drawn between two separate clicks if the view was scrolled in between.
- A flickering brush circle.
- An invalid entry in the size box was kept, and could be painted with. The last good size is used instead.
- Enter after typing a brush size applied the effect and closed the dialog.
- A mesh file that failed to load part way left the distortion half replaced, with no undo.
- A mesh made for an image of a different size was applied a fraction of a pixel out of place.
- A damaged mesh file could put bad values into the mesh that spread as it was brushed.
- An error opening the background menu while another program was using the clipboard.
- The color picker made a see-through color opaque.
- Undo, OK, Load and Save were available while a second stroke was still being applied.
- Mesh files marked read-only couldn't be loaded.
