# Art Studio for DeviantArt

A Chrome extension that adds **Add to Art Studio** to the right-click menu on deviantart.com images and links. It adds
the post as a draft, like pasting its link into Bulk add on the Drafts page.

Clicking the extension's toolbar icon switches between two modes:

- **Add right away** (no badge): the entry reads "Add to Art Studio".
- **Ask for context first** (badge "CTX"): the entry reads "Add to Art Studio with context…" and first asks what the
  vision model should know about the image.

Chrome would put a second entry into a submenu, so there is one entry with a mode instead.

The result shows in the bottom right corner of the page. "Added as draft #N" fades out after 5 seconds; an error (why
the post was refused) stays until you close it.

The entry only shows while Art Studio is running. Chrome rechecks every 30 seconds and whenever you switch tabs, so it
can take that long to appear or disappear after you start or stop Art Studio.

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
