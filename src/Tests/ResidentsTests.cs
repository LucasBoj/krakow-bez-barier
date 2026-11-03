using System.Text.Json;
using Domain.Places;
using Tools;

namespace Tests;

public class ResidentsTests
{
    private static AccessibilityFeature F(FeatureKey key, FeatureState state = FeatureState.Yes, bool demo = false) =>
        new(key, state, null, "test", null, demo);

    private static Place P(string id, PlaceCategory category, double lat, double lon, params AccessibilityFeature[] features) =>
        new(id, "krakow", id, category, lat, lon, null, null, features);

    // Dwie dzielnice: zachodnia (I) i wschodnia (II), każda 0,01° x 0,01° (ok. 1,1 km x 0,7 km).
    private static readonly DistrictMap Districts = new("test", null,
    [
        new District(1, "I", "Zachód", [[50.00, 19.90, 50.00, 19.91, 50.01, 19.91, 50.01, 19.90, 50.00, 19.90]]),
        new District(2, "II", "Wschód", [[50.00, 19.91, 50.00, 19.92, 50.01, 19.92, 50.01, 19.91, 50.00, 19.91]])
    ]);

    [Fact]
    public void Amenities_come_from_category_or_feature_and_ignore_demo_data()
    {
        var shelteredStop = P("s1", PlaceCategory.Stop, 50, 19.9, F(FeatureKey.StopShelter), F(FeatureKey.Benches));
        var bareStop = P("s2", PlaceCategory.Stop, 50, 19.9, F(FeatureKey.StopShelter, FeatureState.No));
        var demoToilet = P("t1", PlaceCategory.Toilet, 50, 19.9, F(FeatureKey.AccessibleToilet, demo: true));

        Assert.True(ResidentsReach.Provides(shelteredStop, Amenity.ShelteredStop));
        Assert.True(ResidentsReach.Provides(shelteredStop, Amenity.Bench));
        Assert.True(ResidentsReach.Provides(bareStop, Amenity.Stop));
        Assert.False(ResidentsReach.Provides(bareStop, Amenity.ShelteredStop));
        Assert.True(ResidentsReach.Provides(demoToilet, Amenity.Toilet));
        Assert.False(ResidentsReach.Provides(demoToilet, Amenity.AccessibleToilet));
    }

    [Fact]
    public void Reach_marks_amenities_within_radius_only()
    {
        // Ławka 0,002° na wschód (ok. 143 m) od pierwszego punktu i 0,006° (ok. 429 m) od drugiego.
        var bench = P("b", PlaceCategory.Bench, 50.005, 19.907);
        var points = new[] { new GeoPoint(50.005, 19.905), new GeoPoint(50.005, 19.901) };

        var reach = ResidentsReach.Compute(points, [bench]);

        Assert.Equal(1 << (int)Amenity.Bench, reach[0]);
        Assert.Equal(0, reach[1]);
    }

    [Fact]
    public void Reach_finds_amenity_in_neighbouring_index_column()
    {
        // 290 m na wschód: przy szerokości 50° to więcej niż jeden kubełek indeksu w stopniach.
        var toilet = P("t", PlaceCategory.Toilet, 50.005, 19.905 + 290 / (111_320.0 * Math.Cos(50.005 * Math.PI / 180)));

        var reach = ResidentsReach.Compute([new GeoPoint(50.005, 19.905)], [toilet]);

        Assert.Equal(1 << (int)Amenity.Toilet, reach[0]);
    }

    private static ResidentsGrid Grid() => new("test", new DateOnly(2024, 6, 30), new DateOnly(2025, 12, 31), 300,
        Lat: [50.005, 50.005, 50.005], Lon: [19.905, 19.915, 19.917], Residents: [100, 300, 100], District: [1, 2, 2],
        Reach: [1 << (int)Amenity.Bench, 0, 1 << (int)Amenity.Bench],
        Districts: [new DistrictResidents(1, "Zachód", 1000, 200, 50), new DistrictResidents(2, "Wschód", 2000, 600, 100)]);

    [Fact]
    public void Report_sums_residents_per_amenity_and_district_and_estimates_seniors_without()
    {
        var report = ResidentsReach.Build(Grid(), Districts);

        Assert.Equal(500, report.Residents);
        Assert.Equal(200, report.Of(Amenity.Bench).WithAccess);
        Assert.Equal(300, report.Of(Amenity.Bench).Without);
        Assert.Equal(0, report.Of(Amenity.Toilet).WithAccess);

        var east = report.Districts.Single(d => d.Number == 2);
        Assert.Equal(400, east.Residents);
        Assert.Equal(0.25, east.Of(Amenity.Bench).Share);
        Assert.Equal(0.3, east.Registry!.SeniorShare);

        // Bez ławki: 0 w Zachodzie i 300 we Wschodzie, w którym seniorzy to 30%.
        Assert.Equal(90, report.Correlations.Single(c => c.Amenity == Amenity.Bench).SeniorsWithoutEstimate);
        Assert.Equal(3000, report.RegistryTotal);
        Assert.Equal(800, report.RegistrySeniors);
    }

    [Fact]
    public void Pearson_matches_known_values_and_refuses_degenerate_input()
    {
        Assert.Equal(1, ResidentsReach.Pearson([1, 2, 3], [2, 4, 6])!.Value, 9);
        Assert.Equal(-1, ResidentsReach.Pearson([1, 2, 3], [3, 2, 1])!.Value, 9);
        Assert.Null(ResidentsReach.Pearson([1, 2], [1, 2]));
        Assert.Null(ResidentsReach.Pearson([1, 1, 1], [1, 2, 3]));
    }

    [Fact]
    public void City_gaps_add_residents_near_priority_places_without_changing_the_score()
    {
        var office = P("office", PlaceCategory.Office, 50.005, 19.915);
        var without = CityGaps.Build([office], Districts, null, []);
        var with = CityGaps.Build([office], Districts, null, [], Grid());

        Assert.Null(without.Priorities.Single().ResidentsNearby);
        Assert.Null(without.Residents);
        // Heksagony 0 i 143 m od urzędu; heksagon w Zachodzie jest 715 m dalej.
        Assert.Equal(400, with.Priorities.Single().ResidentsNearby);
        Assert.Equal(without.Priorities.Single().Score, with.Priorities.Single().Score);
        Assert.NotNull(with.Residents);
    }

    [Fact]
    public void Residents_csv_has_total_row_and_one_row_per_district()
    {
        var report = CityGaps.Build([], Districts, null, [], Grid());

        var lines = CityGapsExport.ResidentsCsv(report).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("nr;dzielnica;mieszkancy_siatka;mieszkancy_ewidencja;proc_seniorow;proc_80_plus;proc_z_przystanek;bez_przystanek", lines[0]);
        Assert.Equal(4, lines.Length);
        Assert.StartsWith(";Razem;500;3000;26,7;5,0;0,0;500", lines[1]);
        Assert.StartsWith("II;Wschód;400;2000;30,0;5,0", lines[3]);
        Assert.Equal("", CityGapsExport.ResidentsCsv(CityGaps.Build([], Districts, null, [])));
    }

    [Fact]
    public void Msip_cell_is_the_hexagon_centre_and_empty_cells_are_skipped()
    {
        using var doc = JsonDocument.Parse("""
            [
              {"type":"Feature","properties":{"r_ogolem":42,"na_dzien_txt":"2024-06-30"},"geometry":{"type":"Polygon","coordinates":[[
                [19.90,50.00],[19.91,50.00],[19.91,50.01],[19.90,50.01],[19.90,50.00]]]}},
              {"type":"Feature","properties":{"r_ogolem":0},"geometry":{"type":"Polygon","coordinates":[[[19.9,50.0],[19.91,50.0],[19.9,50.01],[19.9,50.0]]]}}
            ]
            """);
        var features = doc.RootElement.EnumerateArray().ToList();

        Assert.Equal(new ResidentsCell(50.005, 19.905, 42), MsipResidentsSource.ParseCell(features[0]));
        Assert.Null(MsipResidentsSource.ParseCell(features[1]));
    }

    [Fact]
    public void Msip_district_seniors_combine_post_working_age_groups()
    {
        using var doc = JsonDocument.Parse("""
            [{"attributes":{"id_dz":2,"nazwa_dz":" Dzielnica II Grzegórzki ","r_ogolem":29540,"r_60_65_79":5975,"r_po_80":2286}}]
            """);

        var district = MsipResidentsSource.ParseDistricts(doc.RootElement).Single();

        Assert.Equal(new DistrictResidents(2, "Dzielnica II Grzegórzki", 29540, 8261, 2286), district);
    }
}
