using Domain.Places;

namespace Tests;

public class InvestmentPlannerTests
{
    private static AccessibilityFeature F(FeatureKey key, FeatureState state = FeatureState.Yes) => new(key, state, null, "test", null, false);

    private static Place P(string id, PlaceCategory category, double lat, double lon, string? name = null, params AccessibilityFeature[] features) =>
        new(id, "krakow", name ?? id, category, lat, lon, null, null, features);

    // 0,004° długości to ok. 286 m, 0,01° to ok. 715 m (szerokość 50°).
    // Heksagony: A (100 os.) i B (300 os.) obok siebie, C (50 os.) daleko na wschodzie.
    private static ResidentsGrid Grid() => new("test", new DateOnly(2024, 6, 30), new DateOnly(2025, 12, 31), 300,
        Lat: [50.0, 50.0, 50.0], Lon: [19.900, 19.904, 19.930], Residents: [100, 300, 50], District: [1, 1, 2],
        Reach: [0, 0, 0],
        Districts: [new DistrictResidents(1, "Zachód", 1000, 200, 50), new DistrictResidents(2, "Wschód", 1000, 500, 50)]);

    [Fact]
    public void Candidates_are_existing_objects_without_the_amenity_and_not_customer_toilets()
    {
        Assert.True(InvestmentPlanner.IsCandidate(P("s", PlaceCategory.Stop, 50, 19.9, null, F(FeatureKey.StopShelter, FeatureState.No)), Investment.StopShelter));
        Assert.False(InvestmentPlanner.IsCandidate(P("s", PlaceCategory.Stop, 50, 19.9, null, F(FeatureKey.StopShelter)), Investment.StopShelter));
        Assert.True(InvestmentPlanner.IsCandidate(P("t", PlaceCategory.Toilet, 50, 19.9, "Toaleta publiczna"), Investment.AccessibleToilet));
        Assert.False(InvestmentPlanner.IsCandidate(P("t", PlaceCategory.Toilet, 50, 19.9, "Toaleta dla klientów"), Investment.AccessibleToilet));
        Assert.False(InvestmentPlanner.IsCandidate(P("t", PlaceCategory.Toilet, 50, 19.9, null, F(FeatureKey.AccessibleToilet)), Investment.AccessibleToilet));
    }

    [Fact]
    public void Greedy_plan_counts_only_residents_still_without_access()
    {
        // Przystanek przy A obejmuje A i B, ale B ma już wiatę w zasięgu (przystanek z wiatą 0,006° na wschód od A): zysk to tylko A (100).
        // Przystanek przy C daje 50. Drugi przystanek przy A po pierwszym wyborze nie daje już nic.
        var places = new[]
        {
            P("stop-sheltered", PlaceCategory.Stop, 50.0, 19.906, null, F(FeatureKey.StopShelter)),
            P("stop-a", PlaceCategory.Stop, 50.0, 19.901),
            P("stop-a2", PlaceCategory.Stop, 50.0, 19.900),
            P("stop-c", PlaceCategory.Stop, 50.0, 19.930)
        };

        var plan = InvestmentPlanner.Plan(Investment.StopShelter, Grid(), places);

        Assert.Equal(3, plan.Candidates);
        Assert.Equal(150, plan.WithoutBefore);
        Assert.Equal(2, plan.Picks.Count);
        Assert.Equal("stop-a", plan.Picks[0].Place.Id);
        Assert.Equal(100, plan.Picks[0].Residents);
        Assert.Equal(20, plan.Picks[0].Seniors);
        Assert.Equal("stop-c", plan.Picks[1].Place.Id);
        Assert.Equal(50, plan.Picks[1].Residents);
        Assert.Equal(25, plan.Picks[1].Seniors);
        Assert.Equal(150, plan.Gained);
    }

    [Fact]
    public void Equal_gains_are_broken_by_identifier_and_toilets_get_the_nearest_stop()
    {
        var places = new[]
        {
            P("toilet-b", PlaceCategory.Toilet, 50.0, 19.930, "Toaleta publiczna"),
            P("toilet-a", PlaceCategory.Toilet, 50.0, 19.930, "Toaleta publiczna"),
            P("stop", PlaceCategory.Stop, 50.0, 19.931, "Wschodnia 01")
        };

        var plan = InvestmentPlanner.Plan(Investment.AccessibleToilet, Grid(), places, count: 1);

        Assert.Equal("toilet-a", plan.Picks.Single().Place.Id);
        Assert.Equal("Wschodnia 01", plan.Picks.Single().NearStop);
    }

    [Fact]
    public void Bench_and_tactile_plans_use_stops_that_already_have_them_as_coverage()
    {
        var places = new[]
        {
            P("stop-bench", PlaceCategory.Stop, 50.0, 19.930, null, F(FeatureKey.Benches), F(FeatureKey.TactilePaving, FeatureState.No)),
            P("stop-bare", PlaceCategory.Stop, 50.0, 19.901, null, F(FeatureKey.Benches, FeatureState.No))
        };

        var bench = InvestmentPlanner.Plan(Investment.StopBench, Grid(), places);
        var tactile = InvestmentPlanner.Plan(Investment.StopTactilePaving, Grid(), places);

        // Ławka jest już przy C, więc ławka przy A daje A i B (400); oznaczeń nie ma nigdzie, więc oba przystanki są kandydatami.
        Assert.Equal(400, bench.WithoutBefore);
        Assert.Equal("stop-bare", bench.Picks.Single().Place.Id);
        Assert.Equal(400, bench.Gained);
        Assert.Equal(2, tactile.Candidates);
        Assert.Equal(450, tactile.Gained);
        Assert.Equal("inwestycje-lawki.csv", InvestmentPlanner.FileName(Investment.StopBench));
    }

    [Fact]
    public void Investment_csv_lists_picks_with_gains()
    {
        var plan = InvestmentPlanner.Plan(Investment.StopShelter, Grid(), [P("stop-c", PlaceCategory.Stop, 50.0, 19.930, "Wschodnia 01")]);

        var lines = CityGapsExport.InvestmentCsv(plan).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("pozycja;id;nazwa;adres;najblizszy_przystanek;dzielnica;szer_geogr;dl_geogr;mieszkancy_zyskuja;seniorzy_zyskuja_szacunek;mieszkancy_narastajaco", lines[0]);
        Assert.Equal("1;stop-c;Wschodnia 01;;;;50,000000;19,930000;50;25;50", lines[1]);
    }
}
