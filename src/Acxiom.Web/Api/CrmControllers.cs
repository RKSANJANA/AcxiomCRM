using Acxiom.Application.Common;
using Acxiom.Application.Customers;
using Acxiom.Application.FollowUps;
using Acxiom.Application.Leads;
using Acxiom.Application.Opportunities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Api;

/// <summary>Shared setup: bearer token required, JSON only, errors come back as ProblemDetails.</summary>
[ApiController]
[Authorize(Policy = "Api")]
[Produces("application/json")]
[ProducesResponseType(401)]
[ProducesResponseType(403)]
public abstract class CrmApiController : ControllerBase { }

[Route("api/customers")]
public class CustomersController : CrmApiController
{
    private readonly CustomerHandler _customers;
    public CustomersController(CustomerHandler customers) => _customers = customers;

    /// <summary>Search customers. Query: q, status, owner, sort (e.g. "name" or "-created"), page, size.</summary>
    [HttpGet]
    public Task<Paged<CustomerRow>> List([FromQuery] CustomerFilter filter) => _customers.SearchAsync(filter);

    [HttpGet("{id:int}")]
    [ProducesResponseType(404)]
    public Task<CustomerView> Get(int id) => _customers.GetAsync(id);

    /// <summary>Create a customer. Email and phone must not belong to another customer (409).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerView), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create(CustomerRequest body)
    {
        var id = await _customers.CreateAsync(body.ToDetails(), body.OwnerId);
        return CreatedAtAction(nameof(Get), new { id }, await _customers.GetAsync(id));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<CustomerView> Update(int id, CustomerRequest body)
    {
        await _customers.UpdateAsync(id, body.ToDetails(), body.OwnerId);
        return await _customers.GetAsync(id);
    }

    /// <summary>Soft delete. Admins and managers only; blocked while the customer has opportunities.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Delete(int id)
    {
        await _customers.DeleteAsync(id);
        return NoContent();
    }
}

[Route("api/leads")]
public class LeadsController : CrmApiController
{
    private readonly LeadHandler _leads;
    public LeadsController(LeadHandler leads) => _leads = leads;

    [HttpGet]
    public Task<Paged<LeadRow>> List([FromQuery] LeadFilter filter) => _leads.SearchAsync(filter);

    [HttpGet("{id:int}")]
    [ProducesResponseType(404)]
    public Task<LeadView> Get(int id) => _leads.GetAsync(id);

    /// <summary>Create a lead. It has to start as New or Contacted.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LeadView), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create(LeadRequest body)
    {
        var id = await _leads.CreateAsync(body.ToDetails(), body.Status!.Value, body.OwnerId);
        return CreatedAtAction(nameof(Get), new { id }, await _leads.GetAsync(id));
    }

    /// <summary>Update a lead. Status changes have to follow the workflow (see nextStatuses).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<LeadView> Update(int id, LeadRequest body)
    {
        await _leads.UpdateAsync(id, body.ToDetails(), body.Status!.Value, body.OwnerId);
        return await _leads.GetAsync(id);
    }
}

[Route("api/opportunities")]
public class OpportunitiesController : CrmApiController
{
    private readonly OpportunityHandler _deals;
    public OpportunitiesController(OpportunityHandler deals) => _deals = deals;

    [HttpGet]
    public Task<Paged<OpportunityRow>> List([FromQuery] OpportunityFilter filter) => _deals.SearchAsync(filter);

    [HttpGet("{id:int}")]
    [ProducesResponseType(404)]
    public Task<OpportunityView> Get(int id) => _deals.GetAsync(id);

    /// <summary>Open a new opportunity. Amount &gt; 0, probability 0-100, close date today or later.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OpportunityView), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create(OpportunityRequest body)
    {
        var id = await _deals.CreateAsync(body.ToDetails(), body.Stage!.Value, body.OwnerId);
        return CreatedAtAction(nameof(Get), new { id }, await _deals.GetAsync(id));
    }

    /// <summary>Edit / move stage. Sending stage Won or Lost closes it.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<OpportunityView> Update(int id, OpportunityRequest body)
    {
        await _deals.UpdateAsync(id, body.ToDetails(), body.Stage!.Value, body.OwnerId);
        return await _deals.GetAsync(id);
    }
}

[Route("api/followups")]
public class FollowUpsController : CrmApiController
{
    private readonly FollowUpHandler _followUps;
    public FollowUpsController(FollowUpHandler followUps) => _followUps = followUps;

    /// <summary>List follow-ups. due = today | overdue | upcoming | pending.</summary>
    [HttpGet]
    public Task<Paged<FollowUpRow>> List([FromQuery] FollowUpFilter filter) => _followUps.SearchAsync(filter);

    [HttpGet("{id:int}")]
    [ProducesResponseType(404)]
    public Task<FollowUpRow> Get(int id) => _followUps.GetAsync(id);

    [HttpPost]
    [ProducesResponseType(typeof(FollowUpRow), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create(FollowUpRequest body)
    {
        var id = await _followUps.ScheduleAsync(body.Target!.Value, body.TargetId, body.DueAt!.Value, body.Subject, body.Type, body.Remarks, body.OwnerId);
        return CreatedAtAction(nameof(Get), new { id }, await _followUps.GetAsync(id));
    }
}

[Route("api/reports")]
public class ReportsController : CrmApiController
{
    private readonly OpportunityHandler _deals;
    public ReportsController(OpportunityHandler deals) => _deals = deals;

    /// <summary>Pipeline totals per stage and per owner, limited to what the caller can see.</summary>
    [HttpGet("pipeline")]
    public Task<PipelineSummary> Pipeline() => _deals.PipelineAsync();
}
