using FreightTrack.Web.Dtos;
using FreightTrack.Web.Services;
using FreightTrack.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FreightTrack.Web.Controllers.Api;

/// <summary>
/// REST API for shipments, for other systems (a customer portal, a mobile app, a partner's tracking
/// page) rather than people in a browser.
///
/// How this differs from the MVC ShipmentsController:
///  - It inherits ControllerBase, not Controller: no Razor views, it returns data (JSON) only.
///  - [ApiController] turns on API conventions: invalid request bodies get an automatic 400 with
///    a standard "problem details" JSON body, and [FromBody] is inferred for complex parameters.
///  - Routing is attribute-based ([Route], [HttpGet("...")]) instead of the conventional
///    {controller}/{action}/{id} pattern, so URLs are resource-shaped: /api/shipments/FT-2026-000123.
///  - Errors are HTTP status codes (400, 404) that a program can act on, not a re-rendered form.
///
/// It reuses the same IShipmentService as the MVC controller, which is the payoff of keeping the
/// business rules in the service layer: the API gets identical validation and status rules for free.
/// </summary>
[ApiController]
[Route("api/shipments")]
[Produces("application/json")]
public class ShipmentsApiController(IShipmentService shipments) : ControllerBase
{
    /// <summary>List shipments, with optional filters and paging.</summary>
    /// <remarks>Example: GET /api/shipments?status=InTransit&amp;lateOnly=true&amp;page=1&amp;pageSize=20</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResponse<ShipmentSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ShipmentSummaryDto>>> List([FromQuery] ShipmentFilter filter)
    {
        var result = await shipments.ListAsync(filter);
        var today = shipments.Today;

        return Ok(new PagedResponse<ShipmentSummaryDto>(
            result.Items.Select(s => s.ToSummaryDto(today)).ToList(),
            result.Page, result.PageSize, result.TotalCount, result.TotalPages));
    }

    /// <summary>Get one shipment, with its full status history, by tracking number.</summary>
    /// <response code="200">The shipment was found.</response>
    /// <response code="404">No shipment has that tracking number.</response>
    [HttpGet("{trackingNumber}")]
    [ProducesResponseType<ShipmentDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentDetailDto>> GetByTrackingNumber(string trackingNumber)
    {
        // 1. Look up the shipment by tracking number (the service trims and upper-cases it,
        //    so "ft-2026-000061 " still matches).
        var found = await shipments.FindByTrackingNumberAsync(trackingNumber);

        // 2. Unknown tracking number -> 404 Not Found, which a client program can check for.
        if (found is null)
            return NotFound();

        // 3. FindByTrackingNumberAsync only loads the shipment row. GetDetailsAsync also loads the
        //    customer and the status history (Include), which the detail DTO needs.
        var shipment = await shipments.GetDetailsAsync(found.Id);
        if (shipment is null)
            return NotFound(); // deleted between the two calls: rare, but handled

        // 4. Convert the entity to a DTO and return 200 OK with it as JSON.
        return Ok(shipment.ToDetailDto(shipments.Today));
    }

    /// <summary>Add a status update to a shipment (same rules as the web form).</summary>
    /// <response code="201">The update was recorded. The Location header points to the shipment.</response>
    /// <response code="400">The request broke a rule, e.g. moving the status backwards.</response>
    /// <response code="404">No shipment has that id.</response>
    [HttpPost("{id:int}/status")]
    [ProducesResponseType<StatusUpdateDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatusUpdateDto>> AddStatus(int id, StatusUpdateRequest request)
    {
        // Map the API's request DTO onto the type the service already accepts, so there is
        // exactly one implementation of the status rules.
        var form = new StatusUpdateFormViewModel
        {
            Status = request.Status,
            Location = request.Location,
            Notes = request.Notes,
            UpdatedBy = request.UpdatedBy,
        };

        try
        {
            var update = await shipments.AddStatusUpdateAsync(id, form);

            // 201 Created + a Location header pointing at the changed resource is the REST
            // convention for POST. After SaveChanges, EF has filled in update.Shipment for us.
            return CreatedAtAction(
                nameof(GetByTrackingNumber),
                new { trackingNumber = update.Shipment.TrackingNumber },
                update.ToDto());
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (BusinessRuleException ex)
        {
            // Same shape as the automatic 400 that [ApiController] produces, so clients only have
            // to handle one error format: { "errors": { "Status": ["Status can only move forward..."] } }
            ModelState.AddModelError(ex.Field, ex.Message);
            return ValidationProblem(ModelState);
        }
    }
}
