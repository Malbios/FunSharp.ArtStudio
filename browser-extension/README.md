# Art Studio for DeviantArt

A Chrome extension that adds two entries to the right-click menu on deviantart.com images and links:

- **Add to Art Studio**: adds the post as a draft right away, like pasting its link into Bulk add on the Drafts page.
- **Add to Art Studio with context…**: first asks what the vision model should know about the image.

The entries only show while Art Studio is running. Chrome rechecks every 30 seconds and whenever you switch tabs, so they
can take that long to appear or disappear after you start or stop Art Studio.

## Install

1. Open `chrome://extensions`.
2. Turn on **Developer mode** (top right).
3. Click **Load unpacked** and pick this `browser-extension` folder.

After changing the files, click the reload icon on the extension's card.

## Address

The extension talks to `http://localhost:5180`. To use another address, for example a test instance on 5181, open the
extension's **Details** → **Extension options**.

## Tests

```powershell
cd browser-extension
node --test
```
