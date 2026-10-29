# Art Studio for DeviantArt

A Chrome extension that adds DeviantArt posts to Art Studio as drafts, like pasting their links into Bulk add on the
Drafts page. There are two ways:

- **"+AS" button**: hover over a post's image (a thumbnail, or the image on the post's own page) and click the button
  in its top left corner.
- **Right-click menu**: **Add to Art Studio** on deviantart.com images and links.

Clicking the extension's toolbar icon switches between two modes:

- **Add right away** (no badge): the button reads "+AS" and the entry "Add to Art Studio".
- **Ask for context first** (badge "CTX"): the button reads "+AS…" and the entry "Add to Art Studio with context…";
  both first ask what the vision model should know about the image.

Chrome would put a second entry into a submenu, so there is one entry with a mode instead.

The button already shows what Art Studio would say, from the link alone:

- **"✓AS"** (grey): the post is already in Art Studio; clicking opens that set.
- **"–AS"** (grey): the post is by a blocked user or is your own artwork; clicking does nothing.

For that, the extension fetches the list of known posts and blocked users with its regular check, so a post added
elsewhere (Bulk add, the New set page) turns grey within 30 seconds. DeviantArt is never asked.

The result shows in the bottom right corner of the page. "Added as draft #N" fades out after 5 seconds; an error (why
the post was refused) stays until you close it.

The button and the entry only show while Art Studio is running. Chrome rechecks every 30 seconds and whenever you
switch tabs, so they can take that long to appear or disappear after you start or stop Art Studio.

## Extra picks in DeviantArt's editor

DeviantArt's API takes one image per deviation, so Art Studio uploads pick #1 to Stash. When you open that Stash item
in DeviantArt's editor ("Open in DeviantArt's editor" on the set's Post tab), a box in the bottom right corner (above
DeviantArt's own buttons there) offers to
add picks #2 and up: one click hands them to the editor in pick order, named `02.png`, `03.png` and so on, as if you
had picked them in a file dialog. Picks already in the editor (by those names) are left out, so you can't add one
twice. The extension never touches the main image and never saves or submits; you do that as before.

## Install

1. Open `chrome://extensions`.
2. Turn on **Developer mode** (top right).
3. Click **Load unpacked** and pick this `browser-extension` folder.
4. Pin the extension: click the puzzle icon in Chrome's toolbar, then the pin next to "Art Studio for DeviantArt", so
   its icon (and the mode badge) stays visible.

After changing the files, click the reload icon on the extension's card.

## Address

The extension talks to `http://localhost:5180`. To use another address, for example a test instance on 5181, open the
extension's **Details** → **Extension options**.

## Tests

```powershell
cd browser-extension
node --test
```
