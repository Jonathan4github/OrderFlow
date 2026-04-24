using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Contracts.Orders;
using OrderFlow.Application.Orders.PlaceOrder;

namespace OrderFlow.API.Controllers;

/// <summary>HTTP surface for the Order aggregate.</summary>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController(ISender mediator) : ControllerBase
{
    private readonly ISender _mediator = mediator;

    /// <summary>
    /// Places a new order, reserving stock for each line item. Returns
    /// <c>201 Created</c> on success with the new order's location.
    /// </summary>
    /// <response code="201">Order accepted and is now pending payment processing.</response>
    /// <response code="400">Request body failed structural validation.</response>
    /// <response code="409">One or more items had insufficient stock.</response>
    /// <response code="422">A referenced product could not be found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PlaceOrderResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request.ToCommand(), cancellationToken);
        return CreatedAtAction(nameof(PlaceOrder), new { id = result.OrderId }, result);
    }
}