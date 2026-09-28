using AircraftMRO.Application.Common.Paging;
using AircraftMRO.Application.Features.WorkOrders.DTOs;
using AircraftMRO.Domain.Enums.Aircraft;
using AircraftMRO.Domain.Enums.WorkOrders;
using AircraftMRO.Web.Features.WorkOrders.Models;

namespace AircraftMRO.Tests.Web;

public sealed class WorkOrderListViewModelTests
{
    private static readonly Guid AircraftId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static WorkOrderListViewModel Model(WorkOrderListFilter filter) => new(
        new PagedResponse<WorkOrderListItemDto>([], 1, 20, 0),
        WorkOrderStatisticsDto.Empty,
        filter,
        [new AircraftOptionDto(AircraftId, "HZ-ABC", AircraftStatus.Active)]);

    [Fact]
    public void Pager_links_keep_every_current_filter()
    {
        var model = Model(new WorkOrderListFilter("brake", WorkOrderStatus.OnHold, WorkOrderPriority.High, AircraftId, openOnly: true, overdueOnly: true));

        var values = model.RouteValues(page: 3);

        Assert.Equal(new Dictionary<string, string>
        {
            ["search"] = "brake",
            ["aircraftId"] = AircraftId.ToString(),
            ["status"] = "OnHold",
            ["priority"] = "High",
            ["open"] = "true",
            ["overdue"] = "true",
            ["page"] = "3"
        }, values);
    }

    [Fact]
    public void Tile_links_keep_search_and_aircraft_but_replace_the_facets_and_reset_the_page()
    {
        var model = Model(new WorkOrderListFilter("brake", WorkOrderStatus.OnHold, aircraftId: AircraftId, overdueOnly: true));

        var critical = model.RouteValues(priority: WorkOrderPriority.Critical, openOnly: true, keepFacets: false);
        var all = model.RouteValues(keepFacets: false);

        Assert.Equal(new Dictionary<string, string>
        {
            ["search"] = "brake",
            ["aircraftId"] = AircraftId.ToString(),
            ["priority"] = "Critical",
            ["open"] = "true"
        }, critical);
        Assert.Equal(["aircraftId", "search"], all.Keys.Order());
    }

    [Fact]
    public void Exactly_one_facet_matches_the_current_filter()
    {
        var model = Model(new WorkOrderListFilter(priority: WorkOrderPriority.Critical, openOnly: true));

        Assert.True(model.IsFacet(priority: WorkOrderPriority.Critical, openOnly: true));
        Assert.False(model.IsFacet());
        Assert.False(model.IsFacet(priority: WorkOrderPriority.Critical));
        Assert.False(model.IsFacet(overdueOnly: true));
        Assert.True(Model(WorkOrderListFilter.None).IsFacet());
    }

    [Fact]
    public void Selected_aircraft_registration_comes_from_the_options()
    {
        Assert.Equal("HZ-ABC", Model(new WorkOrderListFilter(aircraftId: AircraftId)).SelectedAircraftRegistration);
        Assert.Null(Model(new WorkOrderListFilter(aircraftId: Guid.NewGuid())).SelectedAircraftRegistration);
        Assert.Null(Model(WorkOrderListFilter.None).SelectedAircraftRegistration);
    }
}
