# Demo video generator

Records the narrated FreightTrack demo video: Playwright drives Microsoft Edge through the app while
[edge-tts](https://github.com/rany2/edge-tts) narrates with the South African voice `en-ZA-LukeNeural`
(an online Microsoft neural voice, so an internet connection is needed). Each scene lasts as long as its
narration, then ffmpeg mixes the clips onto the recording as an MP4.

## Prerequisites

- Python 3.10+, [ffmpeg](https://ffmpeg.org/) on PATH, Microsoft Edge
- `pip install playwright edge-tts` (Playwright uses the installed Edge, so you don't need `playwright install`)

## Run

Record against a **separate demo database** so the scripted updates don't touch your real data:

```powershell
# 1. Start the app on a fresh demo database (it is migrated and seeded on startup)
$env:ConnectionStrings__DefaultConnection = "Server=.\SQLEXPRESS;Database=FreightTrack_Demo;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project src/FreightTrack.Web --urls http://localhost:5235

# 2. In a second terminal
python tools/demo-video/make_demo.py tools/demo-video/work FreightTrack-demo.mp4
```

To record again, drop the `FreightTrack_Demo` database first so it is reseeded. The narration text is in
the `NARRATION` dictionary at the top of the script.
