"""
Records a narrated demo video of FreightTrack.
  1. Narration per scene -> MP3 via edge-tts (Microsoft online neural voice en-ZA-LukeNeural).
  2. Playwright drives Edge through the app and records video; each scene lasts as long as its narration.
  3. ffmpeg places each clip at its scene's start time and encodes an MP4 (H.264 + AAC).
Usage: python make_demo.py <work_dir> <output.mp4>
"""
import asyncio, json, re, subprocess, sys, time
from pathlib import Path
import edge_tts
from playwright.async_api import async_playwright

WORK = Path(sys.argv[1]); OUT = Path(sys.argv[2])
BASE = "http://localhost:5235"
VOICE, RATE = "en-ZA-LukeNeural", "+4%"
W, H = 1280, 720

NARRATION = {
    "intro": "Hi! This is FreightTrack, a shipment tracking system for a freight company's operations team. "
             "It was built by Mutarisi Chibaya, using C sharp, A S P dot net Core M V C, Entity Framework Core, and Microsoft SQL Server.",
    "tour": "The first time someone opens the app, a short guided tour explains each part of the screen. "
            "There's a tour like this on every page, so new staff can learn the system on their own.",
    "dashboard": "The dashboard is where a manager starts the day. It shows live totals, how many shipments are late, "
                 "how many are at each stage, and the ten most overdue shipments, worst first.",
    "track": "When a customer phones in, staff simply type the tracking number into the search box at the top, "
             "and go straight to that shipment.",
    "journey": "The progress bar shows the journey at a glance. This one crosses a border, so it includes a customs step. "
               "Domestic shipments skip customs automatically. On the right, every update is kept as an audit trail.",
    "update": "Recording where the goods are is the most common task, so it's designed to be quick. "
              "The logical next step is already selected, and the location is filled in from the route. "
              "Type your name once, and press the button.",
    "updated": "And the shipment moves forward. The history now shows who made the update, and when.",
    "skip": "If someone jumps ahead, the app warns them exactly which steps would be skipped. "
            "And status can only ever move forward. The system won't send a shipment backwards, even if the request is forged.",
    "problem": "Problems, like a customs hold or damaged goods, are reported as an exception. "
               "A note is required, so the next person knows what went wrong.",
    "onhold": "The shipment is now flagged as on hold, in red. Once it's sorted out, the journey simply resumes from where it stopped.",
    "list": "The shipment list can be filtered by status, transport mode, customer, or late shipments only, with paging. "
            "Each row has an update button that jumps straight to the update panel.",
    "create": "Booking a new shipment is just as simple. Choosing the transport mode suggests a realistic delivery date, "
              "and a unique tracking number is generated automatically when it's saved.",
    "customer": "Each customer's page lists their shipments, with a summary by status and total weight. "
                "That summary comes from a hand-written SQL Server stored procedure, called safely from the app with a parameter.",
    "guide": "There's also a written guide that explains the workflow and the business rules in plain language.",
    "outro": "Under the hood, the business rules live in a service layer covered by forty six x unit tests. "
             "The database uses Entity Framework Core migrations, plus hand-written T SQL views, a stored procedure, indexes and report queries. "
             "Thanks for watching!",
}

# A visible mouse pointer and click ripple (headless recordings don't show the cursor).
CURSOR_JS = """
(() => {
  if (window.__cursor) return; window.__cursor = true;
  const add = () => {
    const c = document.createElement('div');
    c.style.cssText = 'position:fixed;z-index:2147483647;width:22px;height:22px;margin:-3px 0 0 -3px;pointer-events:none;'
      + 'background:url("data:image/svg+xml;utf8,<svg xmlns=%22http://www.w3.org/2000/svg%22 width=%2222%22 height=%2222%22><path d=%22M3 2l14 8-6 1.5L8 18z%22 fill=%22black%22 stroke=%22white%22 stroke-width=%221.5%22/></svg>") no-repeat;'
      + 'left:-50px;top:-50px;transition:transform .1s';
    document.body.appendChild(c);
    document.addEventListener('mousemove', e => { c.style.left = e.clientX + 'px'; c.style.top = e.clientY + 'px'; }, true);
    document.addEventListener('mousedown', e => {
      c.style.transform = 'scale(.85)';
      const r = document.createElement('div');
      r.style.cssText = `position:fixed;z-index:2147483646;left:${e.clientX-18}px;top:${e.clientY-18}px;width:36px;height:36px;border-radius:50%;`
        + 'background:rgba(255,193,7,.55);pointer-events:none;transition:all .45s ease-out';
      document.body.appendChild(r);
      requestAnimationFrame(() => { r.style.transform = 'scale(1.8)'; r.style.opacity = '0'; });
      setTimeout(() => r.remove(), 500);
    }, true);
    document.addEventListener('mouseup', () => c.style.transform = '', true);
  };
  document.readyState === 'loading' ? document.addEventListener('DOMContentLoaded', add) : add();
})();
"""

def card_html(title, subtitle, lines):
    items = "".join(f"<span>{l}</span>" for l in lines)
    return f"""<html><body style="margin:0;height:100vh;display:flex;align-items:center;justify-content:center;
      background:linear-gradient(135deg,#0f172a,#1e3a8a);font-family:Segoe UI,Arial,sans-serif;color:#fff">
      <div style="text-align:center;max-width:960px">
        <div style="font-size:64px;font-weight:700;letter-spacing:-1px">{title}</div>
        <div style="font-size:26px;opacity:.85;margin:12px 0 36px">{subtitle}</div>
        <div style="display:flex;flex-wrap:wrap;gap:12px;justify-content:center">{items}</div>
      </div>
      <style>span{{background:rgba(255,255,255,.12);border:1px solid rgba(255,255,255,.25);padding:8px 16px;border-radius:999px;font-size:20px}}</style>
    </body></html>"""

def duration(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(path)],
                         capture_output=True, text=True, check=True).stdout
    return float(out.strip())

async def make_audio():
    clips = {}
    for key, text in NARRATION.items():
        mp3 = WORK / f"{key}.mp3"
        if not mp3.exists():
            await edge_tts.Communicate(text, VOICE, rate=RATE).save(str(mp3))
        clips[key] = (mp3, duration(mp3))
        print(f"  {key:10s} {clips[key][1]:5.1f}s")
    return clips


class Recorder:
    def __init__(self, page, clips):
        self.page, self.clips, self.t0, self.cues = page, clips, None, []

    def now(self): return time.monotonic() - self.t0

    async def scene(self, key, actions=None, gap=0.5):
        """Start narration `key` now, run the actions, then wait until the narration has finished."""
        start = self.now()
        self.cues.append((key, start))
        if actions: await actions()
        remaining = start + self.clips[key][1] + gap - self.now()
        if remaining > 0: await self.page.wait_for_timeout(int(remaining * 1000))

    async def move(self, locator):
        await locator.scroll_into_view_if_needed()
        box = await locator.bounding_box()
        await self.page.mouse.move(box["x"] + box["width"] / 2, box["y"] + box["height"] / 2, steps=28)
        await self.page.wait_for_timeout(180)

    async def click(self, locator, pause=350):
        await self.move(locator)
        await self.page.mouse.down(); await self.page.mouse.up()
        await self.page.wait_for_timeout(pause)

    async def click_nav(self, locator):
        """Click something that loads a new page, and wait until that page has really loaded."""
        async with self.page.expect_navigation():
            await self.click(locator, pause=0)
        await self.page.wait_for_load_state()
        await self.page.wait_for_timeout(350)

    async def type(self, locator, text, delay=55):
        await self.click(locator, pause=150)
        await locator.fill("")
        await locator.press_sequentially(text, delay=delay)

    async def scroll(self, dy, steps=12):
        for _ in range(steps):
            await self.page.mouse.wheel(0, dy / steps); await self.page.wait_for_timeout(35)


def problem_location(origin, destination, mode):
    """A believable place for an export customs hold on this route."""
    city = origin.split(",")[0].strip()
    if mode == "Sea": return f"Port of {city}"
    if mode == "Air": return f"{city} Airport Cargo Terminal"
    borders = {"Botswana": "Skilpadshek Border Post", "Zimbabwe": "Beitbridge Border Post",
               "Mozambique": "Lebombo Border Post", "Namibia": "Vioolsdrif Border Post"}
    return borders.get(destination.split(",")[-1].strip(), f"{city} border post")


async def find_demo_shipment(browser):
    """A cross-border shipment that has been collected but isn't late: a clean 'next step' to demo."""
    ctx = await browser.new_context(); page = await ctx.new_page()
    await page.goto(f"{BASE}/Shipments?status=Collected&pageSize=100")
    rows = await page.locator("tbody tr").all()
    best = None
    for row in rows:
        cells = await row.locator("td").all_inner_texts()
        if len(cells) < 6: continue
        route = cells[2]
        o, d = [p.strip() for p in route.split("→")]
        crosses = o.split(",")[-1].strip() != d.split(",")[-1].strip()
        late = "late" in cells[5]
        if crosses:
            best = (cells[0].strip(), o, d, cells[3].strip())
            if not late: break
    await ctx.close()
    if not best: raise SystemExit("No collected cross-border shipment in demo data")
    return best


async def record(clips):
    async with async_playwright() as p:
        browser = await p.chromium.launch(channel="msedge")
        tracking, origin, destination, mode = await find_demo_shipment(browser)
        hold_at = problem_location(origin, destination, mode)
        print("demo shipment:", tracking, origin, "->", destination, mode, "| problem at:", hold_at)

        ctx = await browser.new_context(viewport={"width": W, "height": H}, device_scale_factor=1,
                                        record_video_dir=str(WORK / "raw"), record_video_size={"width": W, "height": H})
        await ctx.add_init_script(CURSOR_JS)
        page = await ctx.new_page()
        page.on("dialog", lambda d: asyncio.ensure_future(d.accept()))
        r = Recorder(page, clips)
        r.t0 = time.monotonic()

        await page.set_content(card_html("FreightTrack", "Shipment tracking for freight operations",
                                         ["C#", "ASP.NET Core MVC", "Entity Framework Core", "SQL Server", "xUnit", "Bootstrap"]))
        await page.evaluate(CURSOR_JS)
        await r.scene("intro", gap=0.8)

        # Dashboard with the first-visit tour
        await page.goto(BASE + "/"); await page.wait_for_selector(".tour-card")
        async def tour():
            for _ in range(3):
                await page.wait_for_timeout(1900)
                await r.click(page.locator('[data-tour-action="next"]'))
        await r.scene("tour", tour)

        async def dashboard():
            await r.click(page.locator('[data-tour-action="end"]'))
            await r.move(page.locator(".stat-card").nth(2)); await page.wait_for_timeout(1500)
            await r.move(page.locator(".status-tile").nth(2)); await page.wait_for_timeout(1500)
            await r.move(page.locator("table a.tracking-number").first); await page.wait_for_timeout(800)
        await r.scene("dashboard", dashboard)

        async def track():
            await r.type(page.locator('input[name="trackingNumber"]'), tracking.lower())
            await page.wait_for_timeout(300)
            await r.click_nav(page.get_by_role("button", name="Track"))
        await r.scene("track", track)

        async def journey():
            await r.move(page.locator(".journey-step.current")); await page.wait_for_timeout(2500)
            await r.move(page.locator(".journey-step").nth(3)); await page.wait_for_timeout(2500)
            await r.move(page.locator("#history .timeline li").first)
        await r.scene("journey", journey)

        async def update():
            await r.move(page.locator('label[for^="status-"]:has(.badge)')); await page.wait_for_timeout(1800)
            await r.move(page.locator('[name="NewUpdate.Location"]')); await page.wait_for_timeout(1800)
            await r.type(page.locator('[name="NewUpdate.UpdatedBy"]'), "Thandi M.", delay=70)
            await r.click_nav(page.locator("#status-submit"))
        await r.scene("update", update)

        async def updated():
            await page.evaluate("window.scrollTo({top: 0, behavior: 'smooth'})"); await page.wait_for_timeout(600)
            await r.move(page.locator(".journey-step.current")); await page.wait_for_timeout(1200)
            await r.move(page.locator("#history .timeline li").first)
        await r.scene("updated", updated)

        async def skip():
            await r.click(page.locator('label[for="status-Delivered"]'))
            await r.move(page.locator("#skip-warning")); await page.wait_for_timeout(2500)
        await r.scene("skip", skip)

        async def problem():
            await r.click(page.locator('label[for="status-Exception"]'))
            await r.type(page.locator('[name="NewUpdate.Location"]'), hold_at, delay=40)
            await r.move(page.locator("#notes-required")); await page.wait_for_timeout(600)
            await r.type(page.locator('[name="NewUpdate.Notes"]'),
                         "Held by customs: commercial invoice missing HS codes. Shipper sending corrected invoice.", delay=22)
            await r.click_nav(page.locator("#status-submit"))
        await r.scene("problem", problem)

        async def onhold():
            await page.evaluate("window.scrollTo({top: 0, behavior: 'smooth'})"); await page.wait_for_timeout(600)
            await r.move(page.locator(".journey-step.current")); await page.wait_for_timeout(1500)
            await r.move(page.locator(".alert-danger").first)
        await r.scene("onhold", onhold)

        async def shipment_list():
            await r.click_nav(page.get_by_role("link", name="Shipments", exact=True).first)
            await r.click(page.locator("#lateOnly"))
            await r.click_nav(page.get_by_role("button", name="Filter"))
            await r.move(page.locator("tbody .badge.text-bg-danger").first); await page.wait_for_timeout(1200)
            await r.move(page.locator("tbody a.btn", has_text="Update").first)
        await r.scene("list", shipment_list)

        async def create():
            await r.click_nav(page.get_by_role("link", name="+ New shipment"))
            await r.click(page.locator('[name="CustomerId"]')); await page.select_option('[name="CustomerId"]', label="Kalahari Trading")
            await r.click(page.locator('[name="Mode"]')); await page.select_option('[name="Mode"]', label="Road")
            await r.move(page.locator("#expected-hint")); await page.wait_for_timeout(900)
            await r.type(page.locator('[name="Origin"]'), "Johannesburg, South Africa", delay=35)
            await r.type(page.locator('[name="Destination"]'), "Gaborone, Botswana", delay=35)
            await r.type(page.locator('[name="WeightKg"]'), "2400", delay=60)
            await r.type(page.locator('[name="DeclaredValue"]'), "185000", delay=60)
            await r.click_nav(page.get_by_role("button", name="Book shipment"))
            await r.move(page.locator(".alert-success"))
        await r.scene("create", create)

        async def customer():
            await r.click_nav(page.locator("dd a", has_text="Kalahari Trading"))
            await r.move(page.locator("code", has_text="usp_GetCustomerShipmentSummary")); await page.wait_for_timeout(2500)
            await r.move(page.locator("tr.fw-semibold"))
        await r.scene("customer", customer)

        async def guide():
            await r.click_nav(page.get_by_role("link", name="Guide"))
            await page.wait_for_timeout(800)
            await r.scroll(700, steps=20); await page.wait_for_timeout(1200)
            await r.scroll(600, steps=20)
        await r.scene("guide", guide)

        await page.set_content(card_html("FreightTrack", "Built by Mutarisi Chibaya",
                                         ["Service layer + 46 xUnit tests", "EF Core migrations", "T-SQL view & stored procedure",
                                          "Indexes & report queries", "Git / GitHub"]))
        await page.evaluate(CURSOR_JS)
        await r.scene("outro", gap=1.5)

        video_path = await page.video.path()
        await ctx.close(); await browser.close()
        return Path(video_path), r.cues


def mux(video, cues, clips):
    inputs = ["-i", str(video)]
    filters, labels = [], []
    for i, (key, start) in enumerate(cues, start=1):
        inputs += ["-i", str(clips[key][0])]
        ms = int(start * 1000)
        filters.append(f"[{i}:a]adelay={ms}|{ms}[a{i}]"); labels.append(f"[a{i}]")
    filters.append("".join(labels) + f"amix=inputs={len(labels)}:normalize=0:dropout_transition=0,volume=1.0[aout]")
    cmd = ["ffmpeg", "-y", "-loglevel", "error", *inputs, "-filter_complex", ";".join(filters),
           "-map", "0:v", "-map", "[aout]", "-c:v", "libx264", "-preset", "slow", "-crf", "20", "-pix_fmt", "yuv420p",
           "-r", "25", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", str(OUT)]
    subprocess.run(cmd, check=True)


async def main():
    WORK.mkdir(parents=True, exist_ok=True)
    print("narration:"); clips = await make_audio()
    print("recording...")
    video, cues = await record(clips)
    (WORK / "cues.json").write_text(json.dumps(cues))
    print("mixing...")
    mux(video, cues, clips)
    print(f"done: {OUT}  ({duration(OUT):.0f}s)")

asyncio.run(main())
