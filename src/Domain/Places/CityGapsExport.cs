using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Domain.Hazards;

namespace Domain.Places;

/// <summary>
/// Eksport raportu "Luki w danych i bariery" do plików dla systemów miasta. CSV: separator średnik, przecinek dziesiętny
/// (tak otwiera je polski Excel), UTF-8. GeoJSON: dzielnice z wskaźnikami, miejsca priorytetowe i potwierdzone bariery (WGS 84).
/// </summary>
public static class CityGapsExport
{
    // Sam format liczb, bez danych kultury: przeglądarka (WebAssembly) nie ma ich wbudowanych.
    private static readonly NumberFormatInfo Pl = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "" };

    private static readonly string[] StatsHeader =
        ["miejsca", "z_wejsciem_dla_wozkow", "proc_z_wejsciem", "bez_danych_o_dostepnosci", "proc_bez_danych",
         "z_danymi_o_toalecie", "proc_z_toaleta", "z_halasem_otoczenia", "proc_z_halasem_otoczenia", "z_halasem_w_miejscu"];

    public static string DistrictsCsv(CityGapsReport report)
    {
        var csv = Header(["nr", "dzielnica", .. StatsHeader, "potwierdzone_bariery"]);
        foreach (var d in report.Districts)
            Row(csv, [d.Roman, d.Name, .. Stats(d.Stats), d.Hazards.ToString(Pl)]);
        return csv.ToString();
    }

    public static string GroupsCsv(CityGapsReport report)
    {
        var csv = Header(["kategoria", .. StatsHeader]);
        Row(csv, ["Razem", .. Stats(report.Total)]);
        foreach (var g in report.Groups)
            Row(csv, [g.Group, .. Stats(g.Stats)]);
        return csv.ToString();
    }

    public static string PrioritiesCsv(CityGapsReport report)
    {
        var csv = Header(["pozycja", "id", "nazwa", "kategoria", "adres", "dzielnica", "szer_geogr", "dl_geogr", "odjazdy_w_300_m", "waga", "wynik",
            "mieszkancy_w_300_m"]);
        var rank = 1;
        foreach (var p in report.Priorities)
            Row(csv, [(rank++).ToString(Pl), p.Place.Id, p.Place.Name, p.Place.Category.ToString(), p.Place.Address ?? "", p.District ?? "",
                p.Place.Lat.ToString("0.000000", Pl), p.Place.Lon.ToString("0.000000", Pl), p.DeparturesNearby.ToString(Pl),
                p.Weight.ToString("0.#", Pl), p.Score.ToString("0.#", Pl), p.ResidentsNearby?.ToString(Pl) ?? ""]);
        return csv.ToString();
    }

    /// <summary>Mieszkańcy dzielnic i udogodnienia w zasięgu; pusty napis bez siatki mieszkańców.</summary>
    public static string ResidentsCsv(CityGapsReport report)
    {
        if (report.Residents is not { } r)
            return "";
        var csv = Header(["nr", "dzielnica", "mieszkancy_siatka", "mieszkancy_ewidencja", "proc_seniorow", "proc_80_plus",
            .. ResidentsReach.All.SelectMany(a => new[] { $"proc_z_{ResidentsReach.Slug(a)}", $"bez_{ResidentsReach.Slug(a)}" })]);
        Row(csv, ["", "Razem", r.Residents.ToString(Pl), r.RegistryTotal.ToString(Pl), Share(r.RegistrySeniors, r.RegistryTotal),
            Share(r.Registry80Plus, r.RegistryTotal), .. Reach(r.Total)]);
        foreach (var d in r.Districts)
            Row(csv, [d.Roman, d.Name, d.Residents.ToString(Pl), d.Registry?.Total.ToString(Pl) ?? "", d.Registry is { } g ? Share(g.PostWorkingAge, g.Total) : "",
                d.Registry is { } h ? Share(h.Age80Plus, h.Total) : "", .. Reach(d.Amenities)]);
        return csv.ToString();
    }

    /// <summary>Kolejne wybory planera inwestycji z zyskiem mieszkańców i seniorów.</summary>
    public static string InvestmentCsv(InvestmentPlan plan)
    {
        var csv = Header(["pozycja", "id", "nazwa", "adres", "najblizszy_przystanek", "dzielnica", "szer_geogr", "dl_geogr", "mieszkancy_zyskuja",
            "seniorzy_zyskuja_szacunek", "mieszkancy_narastajaco"]);
        var rank = 1;
        foreach (var p in plan.Picks)
            Row(csv, [(rank++).ToString(Pl), p.Place.Id, p.Place.Name, p.Place.Address ?? "", p.NearStop ?? "", p.District ?? "",
                p.Place.Lat.ToString("0.000000", Pl), p.Place.Lon.ToString("0.000000", Pl), p.Residents.ToString(Pl), p.Seniors.ToString(Pl),
                p.CumulativeResidents.ToString(Pl)]);
        return csv.ToString();
    }

    private static IEnumerable<string> Reach(IReadOnlyList<AmenityReach> amenities) =>
        amenities.SelectMany(a => new[] { (a.Share * 100).ToString("0.0", Pl), a.Without.ToString(Pl) });

    private static string Share(int count, int total) => (total == 0 ? 0 : count * 100.0 / total).ToString("0.0", Pl);

    public static string GeoJson(CityGapsReport report, DistrictMap? districts, IReadOnlyList<VerifiedHazard> hazards)
    {
        var features = new JsonArray();
        foreach (var d in report.Districts)
        {
            var district = districts?.Districts.FirstOrDefault(x => x.Number == d.Number);
            if (district is null)
                continue;
            var properties = StatsProperties(d.Stats, new JsonObject
            {
                ["rodzaj"] = "dzielnica", ["nr"] = d.Roman, ["nazwa"] = d.Name, ["potwierdzone_bariery"] = d.Hazards
            });
            if (report.Residents?.Districts.FirstOrDefault(x => x.Number == d.Number) is { } reach)
            {
                properties["mieszkancy_siatka"] = reach.Residents;
                if (reach.Registry is { } registry)
                {
                    properties["proc_seniorow"] = Math.Round(registry.SeniorShare * 100, 1);
                    properties["proc_80_plus"] = Math.Round(registry.Age80Share * 100, 1);
                }
                foreach (var a in reach.Amenities)
                    properties[$"proc_z_{ResidentsReach.Slug(a.Amenity)}"] = Math.Round(a.Share * 100, 1);
            }
            features.Add(Feature(Polygon(district), properties));
        }

        var rank = 1;
        foreach (var p in report.Priorities)
            features.Add(Feature(Point(p.Place.Lat, p.Place.Lon), new JsonObject
            {
                ["rodzaj"] = "priorytet", ["pozycja"] = rank++, ["id"] = p.Place.Id, ["nazwa"] = p.Place.Name,
                ["kategoria"] = p.Place.Category.ToString(), ["adres"] = p.Place.Address, ["dzielnica"] = p.District,
                ["odjazdy_w_300_m"] = p.DeparturesNearby, ["waga"] = p.Weight, ["wynik"] = p.Score, ["mieszkancy_w_300_m"] = p.ResidentsNearby
            }));

        foreach (var h in hazards)
            features.Add(Feature(Point(h.Lat, h.Lon), new JsonObject
            {
                ["rodzaj"] = "bariera", ["typ"] = h.Kind.ToString(), ["opis"] = h.Description,
                ["potwierdzona"] = h.VerifiedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            }));

        var collection = new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["metadata"] = new JsonObject
            {
                ["raport"] = "Luki w danych i bariery", ["regula_rankingu"] = CityGaps.RankingRule,
                ["dzien_rozkladu"] = report.TrafficDay?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["regula_mieszkancow"] = report.Residents is null ? null : ResidentsReach.Rule
            },
            ["features"] = features
        };
        return collection.ToJsonString(new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    private static IEnumerable<string> Stats(CoverageStats s) =>
    [
        s.Places.ToString(Pl), s.WithWheelchair.ToString(Pl), Percent(s, s.WithWheelchair), s.WithoutAny.ToString(Pl), Percent(s, s.WithoutAny),
        s.WithToilet.ToString(Pl), Percent(s, s.WithToilet), s.WithAmbientNoise.ToString(Pl), Percent(s, s.WithAmbientNoise), s.WithOnSiteNoise.ToString(Pl)
    ];

    private static JsonObject StatsProperties(CoverageStats s, JsonObject properties)
    {
        properties["miejsca"] = s.Places;
        properties["z_wejsciem_dla_wozkow"] = s.WithWheelchair;
        properties["proc_z_wejsciem"] = Math.Round(s.Share(s.WithWheelchair) * 100, 1);
        properties["bez_danych_o_dostepnosci"] = s.WithoutAny;
        properties["proc_bez_danych"] = Math.Round(s.Share(s.WithoutAny) * 100, 1);
        properties["z_danymi_o_toalecie"] = s.WithToilet;
        properties["z_halasem_otoczenia"] = s.WithAmbientNoise;
        properties["z_halasem_w_miejscu"] = s.WithOnSiteNoise;
        return properties;
    }

    private static string Percent(CoverageStats s, int count) => (s.Share(count) * 100).ToString("0.0", Pl);

    private static StringBuilder Header(IEnumerable<string> columns)
    {
        var csv = new StringBuilder();
        Row(csv, columns);
        return csv;
    }

    private static void Row(StringBuilder csv, IEnumerable<string> cells) =>
        csv.Append(string.Join(';', cells.Select(Escape))).Append("\r\n");

    private static string Escape(string value) =>
        value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static JsonObject Feature(JsonObject geometry, JsonObject properties) =>
        new() { ["type"] = "Feature", ["geometry"] = geometry, ["properties"] = properties };

    private static JsonObject Point(double lat, double lon) =>
        new() { ["type"] = "Point", ["coordinates"] = new JsonArray(Math.Round(lon, 6), Math.Round(lat, 6)) };

    /// <summary>Każdy pierścień jako osobny wielokąt MultiPolygonu. W danych MSIP każda dzielnica to jeden pierścień bez dziur.</summary>
    private static JsonObject Polygon(District district)
    {
        var polygons = new JsonArray();
        foreach (var ring in district.Rings)
        {
            var points = new JsonArray();
            for (var i = 0; i + 1 < ring.Length; i += 2)
                points.Add(new JsonArray(ring[i + 1], ring[i]));
            polygons.Add(new JsonArray(points));
        }
        return new JsonObject { ["type"] = "MultiPolygon", ["coordinates"] = polygons };
    }
}
