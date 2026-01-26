using EventManagementMvc.Services;
using FluentAssertions;
using Xunit;

namespace EventManagementMvc.Tests.Unit;

public class ActiveFilterTests
{
    private class Thing
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
    }

    [Fact]
    public void NonAdmin_Sees_Only_Active()
    {
        var data = new[]
        {
            new Thing { Id = 1, IsActive = true },
            new Thing { Id = 2, IsActive = false },
        }.AsQueryable();

        var result = ActiveFilter.Apply(data, isAdmin: false, x => x.IsActive).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo(new[] { 1 });
    }

    [Fact]
    public void Admin_Sees_All()
    {
        var data = new[]
        {
            new Thing { Id = 1, IsActive = true },
            new Thing { Id = 2, IsActive = false },
        }.AsQueryable();

        var result = ActiveFilter.Apply(data, isAdmin: true, x => x.IsActive).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }
}
