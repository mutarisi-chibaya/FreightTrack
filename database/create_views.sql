/*
  create_views.sql
  ----------------
  vw_LateShipments: every shipment that is late, with the customer's name and how many days overdue.

  "Late" = not delivered AND today is past the expected delivery date (same rule as the C# code
  in Shipment.IsLateExpression).

  A VIEW is a saved SELECT. Reports and ad-hoc queries can then just say
      SELECT * FROM dbo.vw_LateShipments ORDER BY DaysOverdue DESC;
  instead of repeating the JOIN and the late rule everywhere.

  Notes:
  - CREATE OR ALTER makes the script safe to run repeatedly (SQL Server 2016 SP1+).
  - A view can't contain ORDER BY (without TOP), so callers sort the result themselves.
  - CAST(GETDATE() AS date) strips the time, so we compare whole days.
  - This script is embedded in the app and run by the EF migration "AddReportingViewAndProcedure".
*/
CREATE OR ALTER VIEW dbo.vw_LateShipments
AS
SELECT
    s.Id                    AS ShipmentId,
    s.TrackingNumber,
    c.Id                    AS CustomerId,
    c.CompanyName           AS CustomerName,
    s.Origin,
    s.Destination,
    s.Mode,
    s.CurrentStatus,
    s.ExpectedDeliveryDate,
    DATEDIFF(DAY, s.ExpectedDeliveryDate, CAST(GETDATE() AS date)) AS DaysOverdue
FROM dbo.Shipments AS s
INNER JOIN dbo.Customers AS c
    ON c.Id = s.CustomerId
WHERE s.CurrentStatus <> 'Delivered'
  AND s.ExpectedDeliveryDate < CAST(GETDATE() AS date);
GO
