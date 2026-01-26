using EventManagementMvc.Services;
using FluentAssertions;
using Xunit;

namespace EventManagementMvc.Tests.Unit;

public class EventPermissionServiceTests
{
    [Theory]
    [InlineData(true, null, "owner", false, false, true)]
    [InlineData(false, "u1", "u1", false, false, true)]
    [InlineData(false, "u2", "u1", false, true, true)]
    [InlineData(false, "u2", "u1", false, false, false)]
    [InlineData(false, null, "u1", false, true, false)]
    [InlineData(false, "uX", "u1", true, false, true)]
    public void CanViewEvent_Works_AsExpected(bool isAdmin, string? userId, string? ownerId, bool isActive, bool hasView, bool expected)
    {
        var result = EventPermissionService.CanViewEvent(isAdmin, userId, ownerId, isActive, hasView);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, null, "owner", false, true)]
    [InlineData(false, "u1", "u1", false, true)]
    [InlineData(false, "u2", "u1", true, true)]
    [InlineData(false, "u2", "u1", false, false)]
    [InlineData(false, null, "u1", true, false)]
    public void CanEditEvent_Works_AsExpected(bool isAdmin, string? userId, string? ownerId, bool hasEdit, bool expected)
    {
        var result = EventPermissionService.CanEditEvent(isAdmin, userId, ownerId, hasEdit);
        result.Should().Be(expected);
    }
}
