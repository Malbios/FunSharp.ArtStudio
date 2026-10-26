# Video via Google Drive (prototype)

Renders one MiniMax H3 image-to-video run on ComfyUI (Colab) and downloads the finished video from Google Drive instead of
through ComfyUI, so it doesn't count against ngrok's monthly data transfer. It is not part of Art Studio's build.

`minimax_h3_i2v.json` is the "FirstAndLast" API export. Without `--end` the end frame is removed from it, which gives exactly
the "FirstOnly" export.

## One-time setup

### 1. Google Cloud (OAuth client for your own login)
1. In the [Google Cloud Console](https://console.cloud.google.com/), create a project (any name).
2. *APIs & Services > Library*: enable **Google Drive API**.
3. *APIs & Services > OAuth consent screen*: user type **External**, fill in the required fields, and add your own Google
   account as a **test user**.
4. *APIs & Services > Credentials > Create credentials > OAuth client ID*: application type **Desktop app**.
5. Download the client JSON and save it as `%LOCALAPPDATA%\ArtStudio-prototype\client_secret.json`.

The first run opens the browser to sign in; the login is then cached in `%LOCALAPPDATA%\ArtStudio-prototype\tokens`.
Neither file is inside the repo.

### 2. Colab: let ComfyUI save videos into Drive
In the notebook, after Drive is mounted and before ComfyUI starts (adjust `/content/ComfyUI` to where your notebook installs it):

```
!mkdir -p /content/drive/MyDrive/ArtStudio/video
!rm -rf /content/ComfyUI/output/video && ln -s /content/drive/MyDrive/ArtStudio/video /content/ComfyUI/output/video
```

Only videos go to Drive; Art Studio's images stay in ComfyUI's normal output folder.

## Run

From this folder, preferably while Art Studio's queue is idle (the run waits in the same ComfyUI queue):

```powershell
dotnet run VideoViaDrive.cs -- --comfy https://<your-ngrok-url> --start C:\path\start.png --end C:\path\end.png --prompt "She turns to the camera and smiles."
dotnet run VideoViaDrive.cs -- --comfy https://<your-ngrok-url> --start C:\path\start.png --prompt "She turns to the camera and smiles."
```

Other options: `--seconds 8`, `--full-quality` (no Lightning LoRA, 20 steps instead of 8), `--drive-folder <path>` (default
`ArtStudio/video`), `--dry-run` (only writes the filled workflow to `output\`). `--help` lists them all.

ComfyUI saves the video under the workflow's own prefix, `video/MiniMax_H3` (so `MiniMax_H3_00001_.mp4` and counting up).
The run looks for the file name ComfyUI reports in `/history` in the Drive folder; if `/history` names no file, it takes the
newest `MiniMax_H3…` file created after the job was queued.

The video lands in `output\` here (git-ignored). The run prints what ComfyUI's `/history` reports for the video, the render
time, how long the file took to show up on Drive, its size, and the download time.

## The 7-day login
`drive.readonly` is a restricted scope. While the consent screen's publishing status is **Testing**, Google lets the login
expire after 7 days, and the next run asks you to sign in again. Whether publishing the app without Google's verification
keeps the login longer is one of the things this prototype should find out: *OAuth consent screen > Publish app*, then delete
the `tokens` folder so the next sign-in issues a fresh login. Expect an "unverified app" warning at sign-in.
