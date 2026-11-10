// Makes the "Record an update" panel on the shipment page smarter. Everything here is a convenience:
// the server enforces the real rules, so the page still works (and stays safe) without JavaScript.
(function () {
    const form = document.getElementById("status-form");
    if (!form) return;

    const radios = form.querySelectorAll('input[name="NewUpdate.Status"]');
    const location = form.querySelector('[name="NewUpdate.Location"]');
    const notes = form.querySelector('[name="NewUpdate.Notes"]');
    const updatedBy = form.querySelector('[name="NewUpdate.UpdatedBy"]');
    const skipWarning = document.getElementById("skip-warning");
    const notesRequired = document.getElementById("notes-required");
    const submit = document.getElementById("status-submit");

    // Once the user types their own location, stop replacing it when they change the status.
    let locationEditedByUser = false;
    location.addEventListener("input", () => (locationEditedByUser = true));

    // Remember who is making updates on this computer, so they don't retype their name every time.
    try {
        if (!updatedBy.value) updatedBy.value = localStorage.getItem("ft-updated-by") || "";
    } catch { /* storage blocked: just leave it empty */ }

    const selected = () => form.querySelector('input[name="NewUpdate.Status"]:checked');

    function refresh(statusChangedByUser) {
        const choice = selected();
        if (!choice) return;

        if (statusChangedByUser && !locationEditedByUser && choice.dataset.location) {
            location.value = choice.dataset.location;
        }

        // Warn (but allow) when a choice jumps over normal steps, e.g. Collected → Out for delivery.
        const skips = choice.dataset.skips;
        skipWarning.classList.toggle("d-none", !skips);
        if (skips) skipWarning.textContent = `Heads up: this skips ${skips}. Only continue if those steps really didn't happen.`;

        // A problem report must say what went wrong.
        const isProblem = choice.value === "Exception";
        notesRequired.classList.toggle("d-none", !isProblem);
        notes.required = isProblem;
        notes.placeholder = isProblem ? "What went wrong? e.g. Held at customs: invoice missing HS codes" : "Optional";

        // The button says exactly what will happen.
        const isDelivery = choice.value === "Delivered";
        submit.textContent = isDelivery ? "Mark as delivered" : isProblem ? "Report problem" : `Record: ${choice.dataset.label}`;
        submit.className = "btn w-100 " + (isProblem ? "btn-danger" : isDelivery ? "btn-success" : "btn-primary");
    }

    radios.forEach(r => r.addEventListener("change", () => refresh(true)));

    form.addEventListener("submit", e => {
        const choice = selected();
        if (choice && choice.value === "Delivered" &&
            !confirm("Mark this shipment as delivered?\n\nThis closes the shipment: no further updates can be added.")) {
            e.preventDefault();
            return;
        }
        try { localStorage.setItem("ft-updated-by", updatedBy.value.trim()); } catch { }
    });

    refresh(false);
})();
