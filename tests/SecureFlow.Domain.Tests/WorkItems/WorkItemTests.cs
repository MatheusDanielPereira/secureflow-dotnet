using SecureFlow.Domain.Common;
using SecureFlow.Domain.WorkItems;

namespace SecureFlow.Domain.Tests.WorkItems;

public sealed class WorkItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NormalizesInputAndStartsAsDraft()
    {
        var item = WorkItem.Create("  Access review  ", "  Quarterly review  ", "user-1", Now);

        Assert.Equal("Access review", item.Title);
        Assert.Equal("Quarterly review", item.Description);
        Assert.Equal(WorkItemStatus.Draft, item.Status);
        Assert.Equal(1, item.Version);
        Assert.Equal(Now, item.CreatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    public void Create_RejectsInvalidTitle(string? title)
    {
        var exception = Assert.Throws<DomainException>(
            () => WorkItem.Create(title, "Description", "user-1", Now));

        Assert.Equal("validation.title_length", exception.Code);
    }

    [Fact]
    public void Submit_ByOwnerMovesItemToSubmittedAndIncrementsVersion()
    {
        var item = CreateDraft();

        var submitted = item.Submit("user-1", Now.AddMinutes(1));

        Assert.Equal(WorkItemStatus.Submitted, submitted.Status);
        Assert.Equal(2, submitted.Version);
        Assert.Equal(WorkItemStatus.Draft, item.Status);
    }

    [Fact]
    public void Submit_ByDifferentActorIsForbidden()
    {
        var exception = Assert.Throws<DomainException>(
            () => CreateDraft().Submit("user-2", Now.AddMinutes(1)));

        Assert.Equal("authorization.owner_required", exception.Code);
    }

    [Fact]
    public void Approve_ByOwnerViolatesSeparationOfDuties()
    {
        var submitted = CreateDraft().Submit("user-1", Now.AddMinutes(1));

        var exception = Assert.Throws<DomainException>(
            () => submitted.Approve("user-1", Now.AddMinutes(2)));

        Assert.Equal("authorization.separation_of_duties", exception.Code);
    }

    [Fact]
    public void Approve_ByDifferentActorMovesItemToApproved()
    {
        var submitted = CreateDraft().Submit("user-1", Now.AddMinutes(1));

        var approved = submitted.Approve("approver-1", Now.AddMinutes(2));

        Assert.Equal(WorkItemStatus.Approved, approved.Status);
        Assert.Equal("approver-1", approved.DecidedBy);
        Assert.Equal(3, approved.Version);
    }

    [Fact]
    public void Approve_RequiresSubmittedState()
    {
        var exception = Assert.Throws<DomainException>(
            () => CreateDraft().Approve("approver-1", Now.AddMinutes(1)));

        Assert.Equal("workflow.invalid_transition", exception.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no")]
    public void Reject_RequiresMeaningfulReason(string? reason)
    {
        var submitted = CreateDraft().Submit("user-1", Now.AddMinutes(1));

        var exception = Assert.Throws<DomainException>(
            () => submitted.Reject("approver-1", reason, Now.AddMinutes(2)));

        Assert.Equal("validation.reason_length", exception.Code);
    }

    [Fact]
    public void Reject_RecordsDecisionDetails()
    {
        var submitted = CreateDraft().Submit("user-1", Now.AddMinutes(1));

        var rejected = submitted.Reject(
            "approver-1",
            "Additional evidence is required.",
            Now.AddMinutes(2));

        Assert.Equal(WorkItemStatus.Rejected, rejected.Status);
        Assert.Equal("approver-1", rejected.DecidedBy);
        Assert.Equal("Additional evidence is required.", rejected.DecisionReason);
        Assert.Equal(3, rejected.Version);
    }

    private static WorkItem CreateDraft() =>
        WorkItem.Create("Access review", "Quarterly review", "user-1", Now);
}
