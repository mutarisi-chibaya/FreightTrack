using FreightTrack.Web.Models;

namespace FreightTrack.Web.Data;

/// <summary>
/// Fills an empty database with realistic demo data: 15 customers and 60 shipments with full,
/// rule-consistent status histories. Dates are relative to "today" so the dashboard always has
/// some late and some in-progress shipments. A fixed Random seed makes the data the same every time.
/// All companies and people are fictional.
/// </summary>
public static class SeedData
{
    private record Route(string Origin, string Destination, ShipmentMode Mode, bool CrossesBorder, string Hub);

    private static readonly Route[] Routes =
    [
        // Road: domestic and cross-border into SADC neighbours
        new("Johannesburg, South Africa", "Durban, South Africa", ShipmentMode.Road, false, "Harrismith, South Africa"),
        new("Durban, South Africa", "Johannesburg, South Africa", ShipmentMode.Road, false, "Pietermaritzburg, South Africa"),
        new("Johannesburg, South Africa", "Cape Town, South Africa", ShipmentMode.Road, false, "Bloemfontein, South Africa"),
        new("Cape Town, South Africa", "Gqeberha, South Africa", ShipmentMode.Road, false, "George, South Africa"),
        new("Pretoria, South Africa", "Polokwane, South Africa", ShipmentMode.Road, false, "Mokopane, South Africa"),
        new("Johannesburg, South Africa", "Gaborone, Botswana", ShipmentMode.Road, true, "Skilpadshek Border Post"),
        new("Johannesburg, South Africa", "Harare, Zimbabwe", ShipmentMode.Road, true, "Beitbridge Border Post"),
        new("Durban, South Africa", "Maputo, Mozambique", ShipmentMode.Road, true, "Lebombo Border Post"),
        new("Cape Town, South Africa", "Windhoek, Namibia", ShipmentMode.Road, true, "Vioolsdrif Border Post"),
        // Sea
        new("Shanghai, China", "Durban, South Africa", ShipmentMode.Sea, true, "Port of Singapore"),
        new("Rotterdam, Netherlands", "Cape Town, South Africa", ShipmentMode.Sea, true, "Port of Las Palmas"),
        new("Durban, South Africa", "Rotterdam, Netherlands", ShipmentMode.Sea, true, "Port of Algeciras"),
        new("Singapore, Singapore", "Durban, South Africa", ShipmentMode.Sea, true, "Indian Ocean"),
        new("Hamburg, Germany", "Durban, South Africa", ShipmentMode.Sea, true, "Port of Rotterdam"),
        new("Durban, South Africa", "Mumbai, India", ShipmentMode.Sea, true, "Port Louis, Mauritius"),
        // Air
        new("Johannesburg, South Africa", "London, United Kingdom", ShipmentMode.Air, true, "OR Tambo Cargo Terminal"),
        new("Frankfurt, Germany", "Johannesburg, South Africa", ShipmentMode.Air, true, "Frankfurt Cargo City"),
        new("Dubai, UAE", "Cape Town, South Africa", ShipmentMode.Air, true, "Dubai World Central"),
        new("Johannesburg, South Africa", "Nairobi, Kenya", ShipmentMode.Air, true, "OR Tambo Cargo Terminal"),
        new("Shanghai, China", "Johannesburg, South Africa", ShipmentMode.Air, true, "Shanghai Pudong Cargo"),
        new("Cape Town, South Africa", "Gaborone, Botswana", ShipmentMode.Air, true, "Cape Town Intl Cargo"),
    ];

    private static readonly string[] Clerks = ["Thandi M.", "Sipho N.", "Lerato K.", "Johan v.d. Merwe", "Ayesha P."];

    public static void Initialize(AppDbContext db, TimeProvider time)
    {
        if (db.Customers.Any()) return; // already seeded

        var random = new Random(2026);
        var now = time.GetLocalNow().DateTime;
        var today = now.Date;

        var customers = CreateCustomers(today);
        db.Customers.AddRange(customers);

        var shipments = new List<Shipment>();
        for (int i = 0; i < 60; i++)
        {
            var route = Routes[random.Next(Routes.Length)];
            var transitDays = route.Mode switch
            {
                ShipmentMode.Air => random.Next(3, 8),
                ShipmentMode.Sea => random.Next(25, 41),
                _ => random.Next(2, 7)
            };
            // Sea freight is booked further back so some of it has arrived.
            var maxAge = route.Mode == ShipmentMode.Sea ? 80 : 45;
            var booked = today.AddDays(-random.Next(0, maxAge));
            var expected = booked.AddDays(transitDays);

            var shipment = new Shipment
            {
                Customer = customers[random.Next(customers.Count)],
                Origin = route.Origin,
                Destination = route.Destination,
                Mode = route.Mode,
                WeightKg = route.Mode switch
                {
                    ShipmentMode.Air => random.Next(5, 800),
                    ShipmentMode.Sea => random.Next(2_000, 24_000),
                    _ => random.Next(200, 12_000)
                },
                DeclaredValue = random.Next(5, 2_000) * 1_000m,
                BookedDate = booked,
                ExpectedDeliveryDate = expected,
            };

            BuildHistory(shipment, route, now, random);
            shipments.Add(shipment);
        }

        // Tracking numbers in booking order: FT-2026-000001, FT-2026-000002, ...
        var counters = new Dictionary<int, int>();
        foreach (var s in shipments.OrderBy(s => s.BookedDate))
        {
            var year = s.BookedDate.Year;
            counters[year] = counters.GetValueOrDefault(year) + 1;
            s.TrackingNumber = $"FT-{year}-{counters[year]:D6}";
        }

        db.Shipments.AddRange(shipments);
        db.SaveChanges();
    }

    /// <summary>
    /// Decide how far along the shipment is, then write a history that obeys the business rules
    /// (forward-only, Exception has a note, CurrentStatus = latest update, DeliveredDate set).
    /// </summary>
    private static void BuildHistory(Shipment s, Route route, DateTime now, Random random)
    {
        var today = now.Date;
        var flow = new List<ShipmentStatus> { ShipmentStatus.Booked, ShipmentStatus.Collected, ShipmentStatus.InTransit };
        if (route.CrossesBorder) flow.Add(ShipmentStatus.AtCustoms);
        flow.Add(ShipmentStatus.OutForDelivery);
        flow.Add(ShipmentStatus.Delivered);

        var totalDays = Math.Max(1, (s.ExpectedDeliveryDate - s.BookedDate).Days);
        var elapsed = (today - s.BookedDate).Days;
        var pastDue = today > s.ExpectedDeliveryDate;

        int reachedIndex;
        if (pastDue)
        {
            // Most overdue-by-date shipments were delivered; about 1 in 3 is still stuck (late).
            reachedIndex = random.Next(3) == 0 ? random.Next(1, flow.Count - 1) : flow.Count - 1;
        }
        else
        {
            var progress = (double)elapsed / totalDays;
            reachedIndex = Math.Min(flow.Count - 2, (int)(progress * (flow.Count - 1)));
        }

        // Spread the update timestamps between booking and now (or the delivery date).
        var end = reachedIndex == flow.Count - 1
            ? s.ExpectedDeliveryDate.AddDays(random.Next(-2, 3)).AddHours(14)
            : now.AddHours(-random.Next(1, 12));
        if (end > now) end = now.AddHours(-1);
        var start = s.BookedDate.AddHours(8 + random.Next(0, 3));
        if (end <= start) end = start.AddHours(2);

        for (int i = 0; i <= reachedIndex; i++)
        {
            var status = flow[i];
            var at = reachedIndex == 0 ? start : start + (end - start) * i / reachedIndex;
            s.StatusUpdates.Add(new ShipmentStatusUpdate
            {
                Status = status,
                Location = LocationFor(status, route),
                Notes = NoteFor(status),
                UpdatedBy = Clerks[random.Next(Clerks.Length)],
                UpdatedAt = at,
            });
        }

        var last = s.StatusUpdates[^1];

        // A few in-flight shipments hit a problem.
        if (last.Status is not ShipmentStatus.Delivered && random.Next(8) == 0)
        {
            last = new ShipmentStatusUpdate
            {
                Status = ShipmentStatus.Exception,
                Location = route.CrossesBorder ? route.Hub : LocationFor(last.Status, route),
                Notes = random.Next(2) == 0
                    ? "Held by customs: commercial invoice missing HS codes. Awaiting documents from shipper."
                    : "Two pallets damaged in handling. Photos taken, claim opened with insurer.",
                UpdatedBy = Clerks[random.Next(Clerks.Length)],
                UpdatedAt = last.UpdatedAt.AddHours(6) > now ? now.AddMinutes(-30) : last.UpdatedAt.AddHours(6),
            };
            s.StatusUpdates.Add(last);
        }

        s.CurrentStatus = last.Status;
        if (last.Status == ShipmentStatus.Delivered) s.DeliveredDate = last.UpdatedAt.Date;
    }

    private static string LocationFor(ShipmentStatus status, Route route) => status switch
    {
        ShipmentStatus.Booked or ShipmentStatus.Collected => route.Origin,
        ShipmentStatus.InTransit => route.Hub,
        ShipmentStatus.AtCustoms => route.Mode == ShipmentMode.Road ? route.Hub : route.Destination,
        _ => route.Destination
    };

    private static string? NoteFor(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Booked => "Booking confirmed with customer.",
        ShipmentStatus.Collected => "Collected from shipper's warehouse.",
        ShipmentStatus.AtCustoms => "Documents submitted for clearance.",
        ShipmentStatus.Delivered => "Signed for by consignee.",
        _ => null
    };

    private static List<Customer> CreateCustomers(DateTime today)
    {
        var data = new (string Company, string Contact, string Email, string Phone, string City, string Country)[]
        {
            ("Highveld Mining Supplies", "Pieter Botha", "pieter.botha@highveldmining.example", "+27 11 555 0101", "Johannesburg", "South Africa"),
            ("Karoo Fresh Produce", "Nomsa Dlamini", "nomsa@karoofresh.example", "+27 21 555 0102", "Cape Town", "South Africa"),
            ("Umhlanga Textiles", "Rajesh Naidoo", "rajesh.naidoo@umhlangatextiles.example", "+27 31 555 0103", "Durban", "South Africa"),
            ("Table Bay Wines", "Charlotte Meyer", "charlotte@tablebaywines.example", "+27 21 555 0104", "Stellenbosch", "South Africa"),
            ("Gauteng Auto Parts", "Themba Mokoena", "themba.m@gautengauto.example", "+27 12 555 0105", "Pretoria", "South Africa"),
            ("Limpopo Agri Co-op", "Mpho Maluleke", "mpho@limpopoagri.example", "+27 15 555 0106", "Polokwane", "South Africa"),
            ("Eastern Cape Components", "Andile Jacobs", "andile.jacobs@eccomponents.example", "+27 41 555 0107", "Gqeberha", "South Africa"),
            ("Kalahari Trading", "Kagiso Molefe", "kagiso@kalaharitrading.example", "+267 555 0108", "Gaborone", "Botswana"),
            ("Zambezi Pharma Distributors", "Tendai Moyo", "tendai.moyo@zambezipharma.example", "+263 555 0109", "Harare", "Zimbabwe"),
            ("Maputo Bay Imports", "Lucia Machava", "lucia@maputobay.example", "+258 555 0110", "Maputo", "Mozambique"),
            ("Shanghai Bright Electronics", "Wei Zhang", "wei.zhang@brightelec.example", "+86 21 5555 0111", "Shanghai", "China"),
            ("Maas Machinery", "Daan de Vries", "daan@maasmachinery.example", "+31 10 555 0112", "Rotterdam", "Netherlands"),
            ("Rhine Valley Chemicals", "Katrin Schmidt", "k.schmidt@rhinechem.example", "+49 69 555 0113", "Frankfurt", "Germany"),
            ("Gulf Star Retail", "Omar Haddad", "omar@gulfstar.example", "+971 4 555 0114", "Dubai", "UAE"),
            ("Malabar Spice Exporters", "Priya Iyer", "priya.iyer@malabarspice.example", "+91 22 5555 0115", "Mumbai", "India"),
        };

        return data.Select((d, i) => new Customer
        {
            CompanyName = d.Company,
            ContactName = d.Contact,
            Email = d.Email,
            Phone = d.Phone,
            City = d.City,
            Country = d.Country,
            CreatedAt = today.AddDays(-200 + i * 7).AddHours(9),
        }).ToList();
    }
}
