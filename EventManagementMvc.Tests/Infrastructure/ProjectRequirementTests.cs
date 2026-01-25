using System;
using System.Net;
using System.Threading.Tasks;
using EventManagementMvc.Data;
using EventManagementMvc.Models;
using EventManagementMvc.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventManagementMvc.Tests;

public class ProjectRequirementTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectRequirementTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Public_List_Shows_Only_Active_Entities()
    {
      
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cat = new Category { Name = "TestCategory", IsActive = true };
            db.Categories.Add(cat);
            db.SaveChanges();

            db.Events.Add(new Event
            {
                Name = "ActiveOne",
                IsActive = true,
                CategoryId = cat.Id,
                Date = DateTime.UtcNow.AddDays(7),
                CreatedByUserId = "owner-1"
            });

            db.Events.Add(new Event
            {
                Name = "InactiveOne",
                IsActive = false,
                CategoryId = cat.Id,
                Date = DateTime.UtcNow.AddDays(7),
                CreatedByUserId = "owner-1"
            });

            db.SaveChanges();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

       
        var res = await client.GetAsync("/Events");
        var body = await res.Content.ReadAsStringAsync();

       
        if (res.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Status: {(int)res.StatusCode} {res.StatusCode}\n\n{body}");
        }

      
        if (!body.Contains("ActiveOne"))
        {
            throw new Exception(
                "ActiveOne not found in /Events HTML. First 2000 chars:\n\n" +
                body.Substring(0, Math.Min(body.Length, 2000))
            );
        }

        if (body.Contains("InactiveOne"))
        {
            throw new Exception(
                "InactiveOne SHOULD NOT be visible, but it was found. First 2000 chars:\n\n" +
                body.Substring(0, Math.Min(body.Length, 2000))
            );
        }
    }

    [Fact]
    public async Task NonOwner_Cannot_Edit_Other_Users_Item()
    {
        int eventId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cat = new Category { Name = "TestCategory2", IsActive = true };
            db.Categories.Add(cat);
            db.SaveChanges();

            var created = db.Events.Add(new Event
            {
                Name = "OwnedByOther",
                IsActive = true,
                CategoryId = cat.Id,
                Date = DateTime.UtcNow.AddDays(7),
                CreatedByUserId = "owner-1"
            });

            db.SaveChanges();
            eventId = created.Entity.Id;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserId, "other-user-2");
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserEmail, "other2@test.com");
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRole, "User");

        var res = await client.GetAsync($"/Events/Edit/{eventId}");

        Assert.True(
            res.StatusCode == HttpStatusCode.Forbidden ||
            res.StatusCode == HttpStatusCode.Redirect ||
            res.StatusCode == HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task Admin_Can_Access_Admin_Area()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserId, "admin-1");
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserEmail, "admin@test.com");
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRole, "Admin");

        var res = await client.GetAsync("/Admin");

        if (res.StatusCode == HttpStatusCode.NotFound)
        {
            res = await client.GetAsync("/Admin/Home");
        }

        Assert.True(
            res.StatusCode == HttpStatusCode.OK ||
            res.StatusCode == HttpStatusCode.Redirect
        );
    }
}
