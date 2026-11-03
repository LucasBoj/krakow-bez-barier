using System.Text.Json;
using Domain.Places;

namespace Tools;

/// <summary>Heksagon 100 m z liczbą stałych zameldowań; środek to średnia wierzchołków.</summary>
public sealed record ResidentsCell(double Lat, double Lon, int Residents);

/// <summary>
/// Mieszkańcy z ewidencji ludności w MSIP: stałe zameldowania w dzielnicach według wieku (najnowszy dostępny stan)
/// i w siatce heksagonów 100 m. Pierwsze daje udział seniorów w dzielnicy, drugie rozkład mieszkańców w przestrzeni.
/// </summary>
public sealed class MsipResidentsSource(HttpClient http)
{
    public const string Source = "MSIP Kraków, ewidencja ludności (stałe zameldowania)";

    public sealed record DistrictsSnapshot(DateOnly Date, IReadOnlyList<DistrictResidents> Districts);

    public sealed record GridSnapshot(DateOnly Date, IReadOnlyList<ResidentsCell> Cells);

    public async Task<DistrictsSnapshot> LoadDistrictsAsync(string layerUrl, CancellationToken ct = default)
    {
        // Warstwa trzyma wszystkie kwartały od 2008 r.; bierzemy najnowszy.
        using var dates = await QueryAsync($"{layerUrl}/query?where=1%3D1&outFields=na_dzien_kod&returnGeometry=false&returnDistinctValues=true" +
                                           "&orderByFields=na_dzien_kod+DESC&f=json", layerUrl, ct);
        var latest = dates.RootElement.GetProperty("features").EnumerateArray()
            .Select(f => f.GetProperty("attributes").GetProperty("na_dzien_kod").GetInt32()).Max();

        using var doc = await QueryAsync($"{layerUrl}/query?where=na_dzien_kod%3D{latest}&outFields=id_dz,nazwa_dz,r_ogolem,r_60_65_79,r_po_80" +
                                         "&returnGeometry=false&orderByFields=id_dz&f=json", layerUrl, ct);
        var snapshot = new DistrictsSnapshot(ParseDate(latest), ParseDistricts(doc.RootElement.GetProperty("features")));
        if (snapshot.Districts.Count == 0)
            throw new InvalidOperationException("Ewidencja dzielnic nie zwróciła żadnego wiersza.");
        Console.WriteLine($"MSIP: mieszkańcy {snapshot.Districts.Count} dzielnic na {snapshot.Date:dd.MM.yyyy}, razem {snapshot.Districts.Sum(d => d.Total)}");
        return snapshot;
    }

    public static IReadOnlyList<DistrictResidents> ParseDistricts(JsonElement features) =>
        features.EnumerateArray()
            .Select(f => f.GetProperty("attributes"))
            .Select(a => new DistrictResidents(a.GetProperty("id_dz").GetInt32(), a.GetProperty("nazwa_dz").GetString()!.Trim(),
                a.GetProperty("r_ogolem").GetInt32(), a.GetProperty("r_60_65_79").GetInt32() + a.GetProperty("r_po_80").GetInt32(),
                a.GetProperty("r_po_80").GetInt32()))
            .OrderBy(d => d.Number)
            .ToList();

    public async Task<GridSnapshot> LoadGridAsync(string layerUrl, CancellationToken ct = default)
    {
        var cells = new List<ResidentsCell>();
        DateOnly? date = null;
        for (var offset = 0; ; offset += 1000)
        {
            using var doc = await QueryAsync($"{layerUrl}/query?where=1%3D1&outFields=r_ogolem,na_dzien_txt&outSR=4326&geometryPrecision=6" +
                                             $"&orderByFields=objectid&resultOffset={offset}&resultRecordCount=1000&f=geojson", layerUrl, ct);
            var features = doc.RootElement.GetProperty("features");
            foreach (var feature in features.EnumerateArray())
            {
                if (ParseCell(feature) is { } cell)
                    cells.Add(cell);
                if (date is null && feature.GetProperty("properties").TryGetProperty("na_dzien_txt", out var d) && d.ValueKind == JsonValueKind.String)
                    date = DateOnly.Parse(d.GetString()!);
            }
            if (features.GetArrayLength() < 1000)
                break;
        }

        if (cells.Count == 0 || date is null)
            throw new InvalidOperationException("Siatka zameldowań nie zwróciła żadnego heksagonu.");
        Console.WriteLine($"MSIP: {cells.Count} heksagonów z mieszkańcami na {date:dd.MM.yyyy}, razem {cells.Sum(c => c.Residents)}");
        return new GridSnapshot(date.Value, cells);
    }

    public static ResidentsCell? ParseCell(JsonElement feature)
    {
        if (!feature.TryGetProperty("geometry", out var geometry) || geometry.ValueKind != JsonValueKind.Object)
            return null;
        if (!feature.GetProperty("properties").TryGetProperty("r_ogolem", out var r) || r.ValueKind != JsonValueKind.Number || r.GetInt32() <= 0)
            return null;
        // Zewnętrzny pierścień bez punktu zamykającego (powtórzenia pierwszego).
        var ring = geometry.GetProperty("coordinates")[0].EnumerateArray().Select(p => (Lon: p[0].GetDouble(), Lat: p[1].GetDouble())).ToList();
        if (ring.Count > 1 && ring[0] == ring[^1])
            ring.RemoveAt(ring.Count - 1);
        return new ResidentsCell(Math.Round(ring.Average(p => p.Lat), 5), Math.Round(ring.Average(p => p.Lon), 5), r.GetInt32());
    }

    /// <summary>Siatka do pliku: dzielnica i maska udogodnień w promieniu dla każdego heksagonu.</summary>
    public static ResidentsGrid Build(GridSnapshot grid, DistrictsSnapshot districts, DistrictMap? map, IEnumerable<Place> places)
    {
        var points = grid.Cells.Select(c => new GeoPoint(c.Lat, c.Lon)).ToList();
        var reach = ResidentsReach.Compute(points, places);
        return new ResidentsGrid(Source, grid.Date, districts.Date, ResidentsReach.RadiusM,
            grid.Cells.Select(c => c.Lat).ToList(), grid.Cells.Select(c => c.Lon).ToList(), grid.Cells.Select(c => c.Residents).ToList(),
            points.Select(p => map?.Find(p)?.Number ?? 0).ToList(), reach, districts.Districts);
    }

    private async Task<JsonDocument> QueryAsync(string url, string layerUrl, CancellationToken ct)
    {
        var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            doc.Dispose();
            throw new InvalidOperationException($"MSIP zwrócił błąd dla {layerUrl}: {error}");
        }
        return doc;
    }

    private static DateOnly ParseDate(int yyyymmdd) => new(yyyymmdd / 10000, yyyymmdd / 100 % 100, yyyymmdd % 100);
}
