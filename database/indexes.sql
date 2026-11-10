/*
  indexes.sql
  -----------
  The indexes the app relies on. They are created by the EF Core migration (configured in
  Data/AppDbContext.cs); this script is the T-SQL equivalent, with the reasoning, and is safe to
  re-run (each CREATE is guarded by IF NOT EXISTS).

  Rule of thumb: index the columns you JOIN on, filter on (WHERE) and sort on (ORDER BY) most
  often, but don't index everything: every index slows down INSERT/UPDATE and uses disk.
*/

-- Tracking numbers must be unique, and looking one up is the most common query in the app.
-- UNIQUE makes the database enforce the rule (even under concurrent inserts) and gives an index seek.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Shipments_TrackingNumber')
    CREATE UNIQUE INDEX IX_Shipments_TrackingNumber ON dbo.Shipments (TrackingNumber);

-- Foreign key: "shipments for customer X" (customer details page, the stored procedure) and the
-- JOIN to Customers. SQL Server does NOT index foreign keys automatically (EF Core does it for us).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Shipments_CustomerId')
    CREATE INDEX IX_Shipments_CustomerId ON dbo.Shipments (CustomerId);

-- Status filter + late check: WHERE CurrentStatus = ... / CurrentStatus <> 'Delivered' AND ExpectedDeliveryDate < today.
-- CurrentStatus first, so the index also serves the dashboard's GROUP BY CurrentStatus and the list filter.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Shipments_CurrentStatus_ExpectedDeliveryDate')
    CREATE INDEX IX_Shipments_CurrentStatus_ExpectedDeliveryDate ON dbo.Shipments (CurrentStatus, ExpectedDeliveryDate);

-- Timeline: WHERE ShipmentId = @id ORDER BY UpdatedAt. One composite index covers the FK and the sort.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ShipmentStatusUpdates_ShipmentId_UpdatedAt')
    CREATE INDEX IX_ShipmentStatusUpdates_ShipmentId_UpdatedAt ON dbo.ShipmentStatusUpdates (ShipmentId, UpdatedAt);

-- Customer list is ordered by name.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Customers_CompanyName')
    CREATE INDEX IX_Customers_CompanyName ON dbo.Customers (CompanyName);

-- Not indexed on purpose: the customer search uses LIKE '%text%'. A leading wildcard can't use a
-- B-tree index, and with a few thousand customers a scan is fast anyway. Full-text search would be
-- the next step if this table grew large.
