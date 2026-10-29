# Art Studio for DeviantArt

A Chrome extension that adds DeviantArt posts to Art Studio as drafts, like pasting their links into Bulk add on the
Drafts page. There are two ways:

- **"+ AS" button**: hover over a post's image (a thumbnail, or the image on the post's own page) and click the button
  in its top right corner.
- **Right-click menu**: **Add to Art Studio** on deviantart.com images and links.

Clicking the extension's toolbar icon switches between two modes:

- **Add right away** (no badge): the button reads "+ AS" and the entry "Add to Art Studio".
- **Ask for context first** (badge "CTX"): the button reads "+ AS…" and the entry "Add to Art Studio with context…";
  both first ask what the vision model should know about the image.

Chrome would put a second entry into a submenu, so there is one entry with a mode instead.

The result shows in the bottom right corner of the page. "Added as draft #N" fades out after 5 seconds; an error (why
the post was refused) stays until you close it.

The button and the entry only show while Art Studio is running. Chrome rechecks every 30 seconds and whenever you
switch tabs, so they can take that long to appear or disappear after you start or stop Art Studio.

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
