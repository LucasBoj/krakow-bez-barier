using System.Globalization;
using System.Text;
using System.Text.Json;
using Domain;
using Domain.Places;
using Domain.Transit;

namespace Tools;

/// <summary>
/// Polecenie "report": te same liczby co strona /miasto, policzone z plików katalogu, żeby dało się je odtworzyć
/// bez przeglądarki. Bariery zgłoszone przez mieszkańców są w bazie hosta, więc tu ich nie liczymy.
/// </summary>
public static class CityGapsReportWriter
{
    public static CityGapsReport Build(string dataDir, out DistrictMap? districts) => Build(dataDir, out districts, out _);

    /// <param name="investments">Plany inwestycji (wiaty, toalety); puste bez siatki mieszkańców.</param>
    public static CityGapsReport Build(string dataDir, out DistrictMap? districts, out IReadOnlyList<InvestmentPlan> investments)
    {
        var places = PlaceFiles.Read(Path.Combine(dataDir, "places"));
        var districtsPath = Path.Combine(dataDir, "districts.json");
        districts = File.Exists(districtsPath) ? JsonSerializer.Deserialize<DistrictMap>(File.ReadAllText(districtsPath), DomainJson.Options) : null;
        var transitPath = Path.Combine(dataDir, "transit.json");
        var transit = File.Exists(transitPath) ? JsonSerializer.Deserialize<TransitNetwork>(File.ReadAllText(transitPath), DomainJson.Options) : null;
        var residentsPath = Path.Combine(dataDir, "residents.json");
        var residents = File.Exists(residentsPath) ? JsonSerializer.Deserialize<ResidentsGrid>(File.ReadAllText(residentsPath), DomainJson.Options) : null;
        var map = districts;
        investments = residents is null ? [] : Enum.GetValues<Investment>().Select(i => InvestmentPlanner.Plan(i, residents, places, map)).ToList();
        return CityGaps.Build(places, districts, transit, [], residents);
    }

    public static string Markdown(CityGapsReport r)
    {
        static string P(CoverageStats s, int n) => (s.Share(n) * 100).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + "%";
        static string Row(string name, CoverageStats s) =>
            $"| {name} | {s.Places} | {s.WithWheelchair} ({P(s, s.WithWheelchair)}) | {s.WithoutAny} ({P(s, s.WithoutAny)}) | {s.WithToilet} ({P(s, s.WithToilet)}) | {s.WithAmbientNoise} ({P(s, s.WithAmbientNoise)}) | {s.WithOnSiteNoise} |";

        var md = new StringBuilder();
        md.AppendLine("# Luki w danych i bariery: liczby z raportu");
        md.AppendLine();
        md.AppendLine("Generuje: `dotnet run --project src/Tools -- report krakow` (z plików katalogu w repozytorium). Te same liczby pokazuje strona `/miasto`; na stronie dochodzą bariery potwierdzone przez urząd z bazy hosta.");
        md.AppendLine();
        md.AppendLine("Miejsca to cele wizyt: wszystkie kategorie katalogu poza ławkami, przystankami i kopertami. „Bez danych” nie liczy hałasu otoczenia z mapy, bo mapa obejmuje każde miejsce.");
        md.AppendLine();
        md.AppendLine("| Grupa | Miejsca | Z wejściem dla wózków | Bez danych o dostępności | Z danymi o toalecie | Z hałasem otoczenia | Z hałasem w miejscu |");
        md.AppendLine("|---|---|---|---|---|---|---|");
        md.AppendLine(Row("**Razem**", r.Total));
        foreach (var g in r.Groups)
            md.AppendLine(Row(g.Group, g.Stats));
        md.AppendLine();
        md.AppendLine($"## Dzielnice (poza granicami dzielnic: {r.OutsideDistricts} miejsc)");
        md.AppendLine();
        md.AppendLine("| Dzielnica | Miejsca | Z wejściem dla wózków | Bez danych o dostępności | Z danymi o toalecie | Z hałasem otoczenia | Z hałasem w miejscu |");
        md.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var d in r.Districts)
            md.AppendLine(Row($"{d.Roman} {d.Name}", d.Stats));
        md.AppendLine();
        md.AppendLine($"## 20 miejsc do sprawdzenia w pierwszej kolejności (z {r.PriorityCandidates} kandydatów, rozkład z dnia {r.TrafficDay:dd.MM.yyyy})");
        md.AppendLine();
        md.AppendLine(CityGaps.RankingRule);
        md.AppendLine();
        md.AppendLine("| # | Miejsce | Kategoria | Dzielnica | Odjazdy w 300 m | Wynik | Mieszkańcy w 300 m |");
        md.AppendLine("|---|---|---|---|---|---|---|");
        var rank = 1;
        foreach (var p in r.Priorities)
            md.AppendLine($"| {rank++} | {p.Place.Name}{(p.Place.Address is { } a ? $", {a}" : "")} | {p.Place.Category} | {p.District ?? "–"} | {p.DeparturesNearby} | {p.Score:0} | {p.ResidentsNearby?.ToString() ?? "–"} |");
        if (r.Residents is { } residents)
            AppendResidents(md, residents);
        return md.ToString();
    }

    private static void AppendResidents(StringBuilder md, ResidentsReport r)
    {
        static string P(double share) => (share * 100).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + "%";

        md.AppendLine();
        md.AppendLine($"## Udogodnienia w zasięgu mieszkańców ({r.RadiusM:0} m)");
        md.AppendLine();
        md.AppendLine(ResidentsReach.Rule);
        md.AppendLine();
        md.AppendLine($"Siatka zameldowań: stan na {r.GridDate:dd.MM.yyyy}, {r.Residents} osób. Ewidencja dzielnic: stan na {r.DistrictsDate:dd.MM.yyyy}, " +
                      $"{r.RegistryTotal} osób, w tym {r.RegistrySeniors} seniorów ({P((double)r.RegistrySeniors / r.RegistryTotal)}) i {r.Registry80Plus} osób 80+.");
        md.AppendLine();
        md.AppendLine("| Udogodnienie | Mieszkańcy w zasięgu | Bez udogodnienia | Szacunek: seniorzy bez udogodnienia | Korelacja z udziałem seniorów (r) |");
        md.AppendLine("|---|---|---|---|---|");
        foreach (var a in r.Total)
        {
            var c = r.Correlations.First(x => x.Amenity == a.Amenity);
            md.AppendLine($"| {ResidentsReach.Label(a.Amenity)} | {a.WithAccess} ({P(a.Share)}) | {a.Without} | {c.SeniorsWithoutEstimate} | " +
                          $"{(c.R is { } value ? value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',') : "–")} |");
        }
        md.AppendLine();
        md.AppendLine("| Dzielnica | Mieszkańcy (siatka) | Seniorzy | 80+ | " + string.Join(" | ", ResidentsReach.All.Select(ResidentsReach.Label)) + " |");
        md.AppendLine("|---|---|---|---|" + string.Concat(ResidentsReach.All.Select(_ => "---|")));
        foreach (var d in r.Districts)
            md.AppendLine($"| {d.Roman} {d.Name} | {d.Residents} | {(d.Registry is { } g ? P(g.SeniorShare) : "–")} | {(d.Registry is { } h ? P(h.Age80Share) : "–")} | " +
                          string.Join(" | ", d.Amenities.Select(a => P(a.Share))) + " |");
    }

    public static string InvestmentsMarkdown(IReadOnlyList<InvestmentPlan> plans)
    {
        var md = new StringBuilder();
        md.AppendLine();
        md.AppendLine("## Planer inwestycji");
        md.AppendLine();
        md.AppendLine(InvestmentPlanner.Rule);
        foreach (var plan in plans)
        {
            md.AppendLine();
            md.AppendLine($"### {(plan.Investment == Investment.StopShelter ? "Wiaty na przystankach" : "Dostosowanie toalet publicznych")}: " +
                          $"{plan.Picks.Count} z {plan.Candidates} kandydatów, +{plan.Gained} mieszkańców (ok. {plan.SeniorsGained} seniorów); " +
                          $"bez udogodnienia przed inwestycją: {plan.WithoutBefore} z {plan.Residents}");
            md.AppendLine();
            md.AppendLine("| # | Obiekt | Dzielnica | Mieszkańcy zyskują | Seniorzy (szacunek) | Narastająco |");
            md.AppendLine("|---|---|---|---|---|---|");
            var rank = 1;
            foreach (var p in plan.Picks)
                md.AppendLine($"| {rank++} | {p.Place.Name}{(p.Place.Address is { } a ? $", {a}" : "")}{(p.NearStop is { } s ? $" (przy przystanku {s})" : "")} | {p.District ?? "–"} | {p.Residents} | {p.Seniors} | {p.CumulativeResidents} |");
        }
        return md.ToString();
    }
}
