// Booking form helper: suggest an expected delivery date from the transport mode.
// Only on the Create page, and only until the user picks a date themselves.
(function () {
    const mode = document.querySelector('[name="Mode"]');
    const booked = document.querySelector('[name="BookedDate"]');
    const expected = document.querySelector('[name="ExpectedDeliveryDate"]');
    const hint = document.getElementById("expected-hint");
    if (!mode || !booked || !expected) return;

    // Typical door-to-door transit times. The <select> values are the enum numbers: Air=0, Sea=1, Road=2.
    const transitDays = { "0": { name: "Air", days: 5 }, "1": { name: "Sea", days: 30 }, "2": { name: "Road", days: 4 } };
    let userPickedDate = false;
    expected.addEventListener("input", () => (userPickedDate = true));

    function suggest() {
        const t = transitDays[mode.value];
        if (!t || !booked.value) { hint.textContent = ""; return; }
        hint.textContent = `${t.name} usually takes about ${t.days} days.`;
        if (userPickedDate) return;
        const d = new Date(booked.value + "T00:00:00");
        d.setDate(d.getDate() + t.days);
        expected.value = d.toISOString().slice(0, 10);
        hint.textContent += " Date suggested; change it if needed.";
    }

    mode.addEventListener("change", suggest);
    booked.addEventListener("change", suggest);
})();
