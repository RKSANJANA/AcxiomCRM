using System.ComponentModel.DataAnnotations;
using Acxiom.Application;
using Acxiom.Application.Auditing;
using Acxiom.Application.FollowUps;
using Acxiom.Domain.Auditing;
using Acxiom.Domain.Common;
using Acxiom.Web.Support;
using Microsoft.AspNetCore.Mvc;

namespace Acxiom.Web.Pages.FollowUps;

public class DetailsModel : CrmPage
{
    private readonly FollowUpHandler _followUps;
    private readonly AuditQuery _audit;

    public DetailsModel(FollowUpHandler followUps, AuditQuery audit, ICurrentUser me)
    {
        _followUps = followUps;
        _audit = audit;
        Me = me;
    }

    public class DoneForm
    {
        [StringLength(Rules.NotesMax), Display(Name = "How did it go?")]
        public string? Outcome { get; set; }
    }

    public class MoveForm
    {
        [Required(ErrorMessage = "Pick a new date."), Display(Name = "New date & time")]
        [NotBeforeToday(ErrorMessage = Rules.Messages.FollowUpDate)]
        public DateTime? NewDate { get; set; }
    }

    public ICurrentUser Me { get; }
    public FollowUpRow Item { get; private set; } = null!;
    public List<AuditEntry> History { get; private set; } = new();
    public DoneForm Done { get; set; } = new();
    public MoveForm Move { get; set; } = new();

    public async Task OnGetAsync(int id) => await LoadAsync(id);

    public async Task<IActionResult> OnPostCompleteAsync(int id, [Bind(Prefix = "Done")] DoneForm done)
    {
        Done = done;
        var ok = await TryAsync(() => _followUps.CompleteAsync(id, done.Outcome), "Done");
        if (ok) Toast("Marked as done.");
        return await AfterAsync(id, ok);
    }

    public async Task<IActionResult> OnPostRescheduleAsync(int id, [Bind(Prefix = "Move")] MoveForm move)
    {
        Move = move;
        var ok = await TryAsync(() => _followUps.RescheduleAsync(id, move.NewDate ?? default), "Move");
        if (ok) Toast("Rescheduled.");
        return await AfterAsync(id, ok);
    }

    public async Task<IActionResult> OnPostMissedAsync(int id)
    {
        var ok = await TryAsync(() => _followUps.MarkMissedAsync(id), "");
        if (ok) Toast("Marked as missed.");
        return await AfterAsync(id, ok);
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var ok = await TryAsync(() => _followUps.CancelAsync(id), "");
        if (ok) Toast("Follow-up cancelled.");
        return await AfterAsync(id, ok);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await _followUps.DeleteAsync(id);
        Toast("Follow-up deleted.");
        return RedirectToPage("Index");
    }

    private async Task<IActionResult> AfterAsync(int id, bool ok)
    {
        if (ok) return RedirectToPage(new { id });
        await LoadAsync(id);
        return Page();
    }

    private async Task LoadAsync(int id)
    {
        Item = await _followUps.GetAsync(id);
        History = await _audit.HistoryAsync("FollowUp", id);
        Move.NewDate ??= DateTime.Today.AddDays(1).AddHours(11);
    }
}
