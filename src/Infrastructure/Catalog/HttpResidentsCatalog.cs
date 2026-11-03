using System.Net;
using System.Net.Http.Json;
using Application.Abstractions;
using Domain;
using Domain.Places;

namespace Infrastructure.Catalog;

/// <summary>Siatka mieszkańców ze statycznego pliku data/{miasto}/residents.json, zapisywanego przez import.</summary>
internal sealed class HttpResidentsCatalog(HttpClient http) : IResidentsCatalog
{
    private readonly Dictionary<string, ResidentsGrid?> _grids = [];

    public async Task<ResidentsGrid?> GetAsync(string cityId, CancellationToken ct)
    {
        if (_grids.TryGetValue(cityId, out var cached))
            return cached;
        try
        {
            using var response = await http.GetAsync($"data/{Uri.EscapeDataString(cityId)}/residents.json", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return _grids[cityId] = null;
            response.EnsureSuccessStatusCode();
            return _grids[cityId] = await response.Content.ReadFromJsonAsync<ResidentsGrid>(DomainJson.Options, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
