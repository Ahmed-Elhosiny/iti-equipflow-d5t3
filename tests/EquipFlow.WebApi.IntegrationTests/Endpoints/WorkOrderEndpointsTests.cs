using System.Net;
using System.Net.Http.Json;
using EquipFlow.Application.WorkOrders.Queries;
using EquipFlow.Domain.Entities;
using EquipFlow.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EquipFlow.WebApi.IntegrationTests.Endpoints;

public sealed class WorkOrderEndpointsTests : IClassFixture<EquipFlowWebApplicationFactory>
{
    private static readonly Guid CurrentUserId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly HttpClient client;
    private readonly EquipFlowWebApplicationFactory factory;

    public WorkOrderEndpointsTests(EquipFlowWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetWorkOrderById_ReturnsWorkOrder_WhenExists()
    {
        var workOrder = await AddWorkOrderAsync(CurrentUserId.ToString(), "GET by ID");

        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/workorders/{workOrder.Id}",
            "Technician");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WorkOrderDto>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(workOrder.Id);
        body.Title.Should().Be("GET by ID");
        body.Status.Should().Be(Domain.Enums.WorkOrderStatus.Draft);
        body.CreatedBy.Should().Be(CurrentUserId.ToString());
    }

    [Fact]
    public async Task GetWorkOrdersByUser_ReturnsOnlyUserOrders()
    {
        var otherUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await AddWorkOrderAsync(CurrentUserId.ToString(), "Current user's order");
        await AddWorkOrderAsync(otherUserId.ToString(), "Other user's order");

        using var request = CreateRequest(HttpMethod.Get, "/api/workorders", "Technician");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<WorkOrderDto>>();
        body.Should().NotBeNull();
        body!.Should().OnlyContain(workOrder => workOrder.CreatedBy == CurrentUserId.ToString());
        body.Should().Contain(workOrder => workOrder.Title == "Current user's order");
        body.Should().NotContain(workOrder => workOrder.Title == "Other user's order");
    }

    [Fact]
    public async Task GetPendingApprovals_Returns403_WhenNotSupervisor()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/workorders/pending-approvals",
            "Technician");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<WorkOrder> AddWorkOrderAsync(string createdBy, string title)
    {
        var workOrder = new WorkOrder(title, "Symptom", "Equipment", createdBy);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EquipFlowDbContext>();
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();
        return workOrder;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url,
        string role)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.RoleHeader, role);
        return request;
    }
}
