// A small guided tour, no library needed. Any element can join the tour of its page with:
//   data-tour="What to say" data-tour-title="Heading" data-tour-step="1"
// Start it with any element that has [data-tour-start], by adding ?tour=1 to the URL, or automatically
// on the first visit to a page whose <body> has data-tour-autostart.
(function () {
    const SEEN_KEY = "ft-tour-seen";
    let steps = [], index = 0, spotlight = null, card = null;

    const escapeHtml = s => s.replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    const isVisible = el => el.getClientRects().length > 0;

    function collectSteps() {
        return [...document.querySelectorAll("[data-tour]")]
            .filter(isVisible)
            .sort((a, b) => (+a.dataset.tourStep || 0) - (+b.dataset.tourStep || 0));
    }

    function build() {
        spotlight = document.createElement("div");
        spotlight.className = "tour-spotlight";
        card = document.createElement("div");
        card.className = "tour-card card shadow-lg";
        card.setAttribute("role", "dialog");
        card.setAttribute("aria-live", "polite");
        document.body.append(spotlight, card);
    }

    function place() {
        const rect = steps[index].getBoundingClientRect();
        const pad = 6;
        Object.assign(spotlight.style, {
            top: `${rect.top - pad}px`, left: `${rect.left - pad}px`,
            width: `${rect.width + pad * 2}px`, height: `${rect.height + pad * 2}px`
        });

        const width = Math.min(360, window.innerWidth - 24);
        card.style.width = `${width}px`;
        const height = card.offsetHeight;
        let top = rect.bottom + 14;                                    // prefer below the element
        if (top + height > window.innerHeight - 12) top = rect.top - height - 14; // else above
        if (top < 12) top = Math.max(12, window.innerHeight - height - 12);      // else pin to bottom
        const left = Math.min(Math.max(12, rect.left), window.innerWidth - width - 12);
        card.style.top = `${top}px`;
        card.style.left = `${left}px`;
    }

    function show() {
        const el = steps[index];
        const last = index === steps.length - 1;
        card.innerHTML = `
            <div class="card-body">
                <div class="small text-muted mb-1">Step ${index + 1} of ${steps.length}</div>
                <h2 class="h6 card-title mb-1">${escapeHtml(el.dataset.tourTitle || "")}</h2>
                <p class="card-text small mb-3">${escapeHtml(el.dataset.tour)}</p>
                <div class="d-flex justify-content-between align-items-center">
                    <button type="button" class="btn btn-sm btn-link px-0" data-tour-action="end">Skip tour</button>
                    <div>
                        ${index > 0 ? '<button type="button" class="btn btn-sm btn-outline-secondary me-1" data-tour-action="back">Back</button>' : ""}
                        <button type="button" class="btn btn-sm btn-primary" data-tour-action="next">${last ? "Finish" : "Next"}</button>
                    </div>
                </div>
            </div>`;
        el.scrollIntoView({ block: "center", behavior: "instant" });
        requestAnimationFrame(place);
        card.querySelector('[data-tour-action="next"]').focus({ preventScroll: true });
    }

    function start() {
        steps = collectSteps();
        if (steps.length === 0) return;
        if (!spotlight) build();
        spotlight.hidden = card.hidden = false;
        index = 0;
        show();
        try { localStorage.setItem(SEEN_KEY, "1"); } catch { }
    }

    function end() {
        if (spotlight) spotlight.hidden = card.hidden = true;
    }

    const isOpen = () => card && !card.hidden;

    document.addEventListener("click", e => {
        if (e.target.closest("[data-tour-start]")) { e.preventDefault(); start(); return; }
        const action = e.target.closest("[data-tour-action]")?.dataset.tourAction;
        if (!action || !isOpen()) return;
        if (action === "next") { if (index < steps.length - 1) { index++; show(); } else end(); }
        else if (action === "back") { index--; show(); }
        else end();
    });
    document.addEventListener("keydown", e => {
        if (!isOpen()) return;
        if (e.key === "Escape") end();
        if (e.key === "ArrowRight" && index < steps.length - 1) { index++; show(); }
        if (e.key === "ArrowLeft" && index > 0) { index--; show(); }
    });
    window.addEventListener("resize", () => isOpen() && place());
    window.addEventListener("scroll", () => isOpen() && place(), { passive: true });

    window.freightTour = { start, end };

    let seen = false;
    try { seen = !!localStorage.getItem(SEEN_KEY); } catch { }
    const forced = new URLSearchParams(location.search).has("tour");
    if (forced || ("tourAutostart" in document.body.dataset && !seen)) {
        window.addEventListener("load", start);
    }
})();
