/*
  stored_procedures.sql
  ---------------------
  usp_GetCustomerShipmentSummary @CustomerId
  For one customer: how many shipments are in each status and the total weight, plus a
  grand-total row. Called from the app on the Customer Details page (CustomerService.GetShipmentSummaryAsync).

  How it works:
  - GROUP BY ROLLUP(CurrentStatus) returns one row per status AND an extra "super-aggregate" row
    where CurrentStatus is NULL: the totals across all statuses. One query, no UNION needed.
  - GROUPING(CurrentStatus) is 1 on that total row, which we use to sort it last.
  - SET NOCOUNT ON stops the "(n rows affected)" message, a standard habit in procedures.

  Why a stored procedure here (instead of LINQ)?
  - Shows the SQL skill the role asks for, and the query plan is cached on the server.
  - The app calls it with a parameter (@CustomerId), so it's safe from SQL injection.

  Example:  EXEC dbo.usp_GetCustomerShipmentSummary @CustomerId = 1;
*/
CREATE OR ALTER PROCEDURE dbo.usp_GetCustomerShipmentSummary
    @CustomerId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.CurrentStatus                                  AS [Status],         -- NULL on the total row
        COUNT(*)                                         AS ShipmentCount,
        CAST(SUM(s.WeightKg) AS DECIMAL(18, 2))          AS TotalWeightKg
    FROM dbo.Shipments AS s
    WHERE s.CustomerId = @CustomerId                       -- uses index IX_Shipments_CustomerId
    GROUP BY ROLLUP (s.CurrentStatus)
    ORDER BY GROUPING(s.CurrentStatus), s.CurrentStatus;
END
GO
