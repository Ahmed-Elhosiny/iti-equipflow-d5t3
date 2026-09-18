using System.Net;
using System.Net.Http.Json;
using EquipFlow.Application.Budget.Ports;
using EquipFlow.Application.CostGovernor.Queries.Dtos;
using EquipFlow.Domain.Budget;
using EquipFlow.Domain.Budget.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EquipFlow.WebApi.IntegrationTests.Endpoints;

public sealed class CostGovernorEndpointsTests : IClassFixture<EquipFlowWebApplicationFactory>
{
    private static readonly Guid CurrentUserId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly HttpClient client;
    private readonly EquipFlowWebApplicationFactory factory;

    public CostGovernorEndpointsTests(EquipFlowWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetBudgetMe_ReturnsBudget_WhenAuthenticated()
    {
        var budget = new UserBudget(CurrentUserId, Money.FromDecimal(100m));
        var reservationId = Guid.NewGuid();
        budget.TryReserve(reservationId, Money.FromDecimal(10m)).Should().BeTrue();
        budget.Commit(reservationId, Money.FromDecimal(4m));

        var repository = factory.Services.GetRequiredService<IUserBudgetRepository>();
        await repository.AddAsync(budget, CancellationToken.None);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/budget/me");
        request.Headers.Add(TestAuthHandler.RoleHeader, "Technician");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BudgetSummaryDto>();
        body.Should().NotBeNull();
        body!.Consumed.Should().Be(4m);
        body.Available.Should().Be(96m);
    }

    [Fact]
    public async Task GetBudgetMe_Returns401_WhenUnauthenticated()
    {
        using var response = await client.GetAsync("/api/budget/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}