using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightTrack.Web.Data.Migrations
{
    /// <summary>
    /// A hand-edited migration: EF doesn't generate views or stored procedures, so we run our own
    /// T-SQL from /database (embedded in the assembly). EF's SQL Server provider splits the scripts
    /// on "GO" lines, the same way SSMS does.
    /// </summary>
    public partial class AddReportingViewAndProcedure : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlScripts.Read("create_views.sql"));
            migrationBuilder.Sql(SqlScripts.Read("stored_procedures.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_GetCustomerShipmentSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS dbo.vw_LateShipments;");
        }
    }
}
