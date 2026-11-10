/*
  reports.sql
  -----------
  Example management reports, written by hand in T-SQL. Run them in SSMS or the VS Code
  "SQL Server (mssql)" extension against the FreightTrack database.
*/

-- ============================================================================================
-- 1. Top 5 customers by number of shipments booked this year
--    Techniques: INNER JOIN, GROUP BY, date filter, TOP + ORDER BY
-- ============================================================================================
SELECT TOP (5)
    c.CompanyName,
    c.Country,
    COUNT(*)         AS ShipmentsThisYear,
    SUM(s.WeightKg)  AS TotalWeightKg
FROM dbo.Shipments AS s
INNER JOIN dbo.Customers AS c ON c.Id = s.CustomerId
-- A range on the raw column (not YEAR(s.BookedDate) = ...) stays "sargable": SQL Server can still
-- use an index on BookedDate, because we aren't wrapping the column in a function.
WHERE s.BookedDate >= DATEFROMPARTS(YEAR(GETDATE()), 1, 1)
  AND s.BookedDate <  DATEFROMPARTS(YEAR(GETDATE()) + 1, 1, 1)
GROUP BY c.Id, c.CompanyName, c.Country
ORDER BY ShipmentsThisYear DESC, TotalWeightKg DESC;


-- ============================================================================================
-- 2. Average delivery time (in days) by transport mode, delivered shipments only
--    Techniques: aggregate over a computed column, AVG of DATEDIFF, filtering NULLs
-- ============================================================================================
SELECT
    s.Mode,
    COUNT(*)                                                         AS DeliveredShipments,
    -- multiply by 1.0 so AVG works in decimals instead of rounding to whole days
    CAST(AVG(DATEDIFF(DAY, s.BookedDate, s.DeliveredDate) * 1.0) AS DECIMAL(6, 1)) AS AvgDaysToDeliver,
    MIN(DATEDIFF(DAY, s.BookedDate, s.DeliveredDate))                AS FastestDays,
    MAX(DATEDIFF(DAY, s.BookedDate, s.DeliveredDate))                AS SlowestDays
FROM dbo.Shipments AS s
WHERE s.DeliveredDate IS NOT NULL
GROUP BY s.Mode
ORDER BY AvgDaysToDeliver;


-- ============================================================================================
-- 3. On-time delivery rate per customer
--    Techniques: CTE (WITH ...), CASE inside an aggregate, LEFT JOIN so customers with
--    no deliveries still appear
-- ============================================================================================
WITH Delivered AS (
    SELECT
        s.CustomerId,
        CASE WHEN s.DeliveredDate <= s.ExpectedDeliveryDate THEN 1 ELSE 0 END AS OnTime
    FROM dbo.Shipments AS s
    WHERE s.CurrentStatus = 'Delivered'
)
SELECT
    c.CompanyName,
    COUNT(d.CustomerId)                         AS DeliveredCount,
    SUM(d.OnTime)                               AS OnTimeCount,
    CAST(100.0 * SUM(d.OnTime) / NULLIF(COUNT(d.CustomerId), 0) AS DECIMAL(5, 1)) AS OnTimePercent
FROM dbo.Customers AS c
LEFT JOIN Delivered AS d ON d.CustomerId = c.Id
GROUP BY c.Id, c.CompanyName
ORDER BY OnTimePercent DESC;     -- NULLIF avoids divide-by-zero for customers with no deliveries


-- ============================================================================================
-- 4. Shipments currently held up: the latest status update of every shipment in Exception,
--    with the note explaining why
--    Techniques: correlated subquery to find each shipment's most recent update
-- ============================================================================================
SELECT
    s.TrackingNumber,
    c.CompanyName,
    u.Location,
    u.Notes,
    u.UpdatedBy,
    u.UpdatedAt
FROM dbo.Shipments AS s
INNER JOIN dbo.Customers AS c ON c.Id = s.CustomerId
INNER JOIN dbo.ShipmentStatusUpdates AS u ON u.ShipmentId = s.Id
WHERE s.CurrentStatus = 'Exception'
  AND u.UpdatedAt = (SELECT MAX(u2.UpdatedAt)           -- the subquery runs per shipment
                     FROM dbo.ShipmentStatusUpdates AS u2
                     WHERE u2.ShipmentId = s.Id)
ORDER BY u.UpdatedAt;


-- ============================================================================================
-- 5. Monthly volume by mode for the last 6 months
--    Techniques: date filter with DATEADD, grouping by a derived month, window function
--    (SUM ... OVER) for each month's total so we can show a percentage share
-- ============================================================================================
SELECT
    FORMAT(DATEFROMPARTS(YEAR(s.BookedDate), MONTH(s.BookedDate), 1), 'yyyy-MM') AS [Month],
    s.Mode,
    COUNT(*) AS Shipments,
    CAST(100.0 * COUNT(*) / SUM(COUNT(*)) OVER (PARTITION BY YEAR(s.BookedDate), MONTH(s.BookedDate))
         AS DECIMAL(5, 1)) AS PercentOfMonth
FROM dbo.Shipments AS s
WHERE s.BookedDate >= DATEADD(MONTH, -6, CAST(GETDATE() AS date))
GROUP BY YEAR(s.BookedDate), MONTH(s.BookedDate), s.Mode
ORDER BY [Month], s.Mode;


-- ============================================================================================
-- Bonus: using the view from create_views.sql
-- ============================================================================================
SELECT TOP (10) TrackingNumber, CustomerName, CurrentStatus, DaysOverdue
FROM dbo.vw_LateShipments
ORDER BY DaysOverdue DESC;
