namespace Domain.Places;

/// <summary>Rodzaj inwestycji: doposażenie istniejącego obiektu, które daje udogodnienie z <see cref="Amenity"/>.</summary>
public enum Investment
{
    /// <summary>Wiata na przystanku, który jej nie ma.</summary>
    StopShelter,

    /// <summary>Dostosowanie istniejącej toalety publicznej dla osób z niepełnosprawnościami.</summary>
    AccessibleToilet,

    /// <summary>Ławka na przystanku, który jej nie ma (ważne dla seniorów i osób, które nie mogą długo stać).</summary>
    StopBench,

    /// <summary>Oznaczenia dotykowe (ścieżki, pola uwagi) na przystanku, który ich nie ma (ważne dla osób niewidomych).</summary>
    StopTactilePaving
}

/// <param name="Place">Istniejący obiekt do doposażenia.</param>
/// <param name="Residents">Mieszkańcy, którzy zyskają udogodnienie w zasięgu dzięki temu wyborowi (po wcześniejszych wyborach).</param>
/// <param name="Seniors">Szacunek: zyskujący mieszkańcy × udział seniorów w ich dzielnicy.</param>
/// <param name="CumulativeResidents">Suma zyskujących po tym i wszystkich wcześniejszych wyborach.</param>
/// <param name="District">Dzielnica obiektu; null poza dzielnicami albo bez granic.</param>
/// <param name="NearStop">Najbliższy przystanek (do 300 m), żeby obiekt bez adresu dało się znaleźć w terenie; null, gdy obiekt sam jest przystankiem.</param>
public sealed record InvestmentPick(Place Place, int Residents, int Seniors, int CumulativeResidents, string? District, string? NearStop = null);

/// <param name="Candidates">Liczba obiektów, które można doposażyć.</param>
/// <param name="WithoutBefore">Mieszkańcy bez udogodnienia w zasięgu przed inwestycjami.</param>
public sealed record InvestmentPlan(Investment Investment, int Candidates, int Residents, int WithoutBefore, IReadOnlyList<InvestmentPick> Picks)
{
    public int Gained => Picks.Count == 0 ? 0 : Picks[^1].CumulativeResidents;

    public int SeniorsGained => Picks.Sum(p => p.Seniors);
}

/// <summary>
/// Planer inwestycji: które istniejące obiekty doposażyć najpierw, żeby jak najwięcej mieszkańców zyskało udogodnienie
/// w promieniu <see cref="ResidentsReach.RadiusM"/>. Zachłanny wybór maksymalnego pokrycia: każdy kolejny obiekt liczy
/// tylko mieszkańców, którzy po wcześniejszych wyborach nadal nie mają udogodnienia w zasięgu.
/// </summary>
public static class InvestmentPlanner
{
    public const int PickCount = 10;

    public const string Rule =
        "Kandydaci to wyłącznie istniejące obiekty: przystanki bez wiaty (brak wiaty w rejestrze ZTP; OSM podaje brak albo nie podaje nic), " +
        "bez ławki albo bez oznaczeń dotykowych (według OSM: brak albo brak informacji) oraz toalety publiczne bez potwierdzonego dostosowania " +
        "dla osób z niepełnosprawnościami. Dla każdego kandydata liczymy stałych mieszkańców " +
        "heksagonów 100 m, których środek leży do 300 m od obiektu i którzy dziś nie mają tego udogodnienia w zasięgu. Wybieramy po kolei obiekt " +
        "z największym takim zyskiem; po każdym wyborze jego mieszkańcy nie liczą się już u kolejnych kandydatów (zachłanny wybór maksymalnego pokrycia). " +
        "Seniorzy to szacunek: zyskujący mieszkańcy × udział seniorów w dzielnicy. Ranking nie uwzględnia kosztu ani możliwości technicznych.";

    /// <summary>Obiekt, który już daje to, co inwestycja: przystanek z wiatą, ławką albo oznaczeniami, toaleta dostosowana.</summary>
    public static bool Provides(Place place, Investment investment) => investment switch
    {
        Investment.StopShelter => ResidentsReach.Provides(place, Amenity.ShelteredStop),
        Investment.AccessibleToilet => ResidentsReach.Provides(place, Amenity.AccessibleToilet),
        Investment.StopBench => place.Category == PlaceCategory.Stop && Yes(place, FeatureKey.Benches),
        Investment.StopTactilePaving => place.Category == PlaceCategory.Stop && Yes(place, FeatureKey.TactilePaving),
        _ => false
    };

    /// <summary>Nazwa pliku CSV z planem (strona i polecenie report).</summary>
    public static string FileName(Investment investment) => investment switch
    {
        Investment.StopShelter => "inwestycje-wiaty.csv",
        Investment.AccessibleToilet => "inwestycje-toalety.csv",
        Investment.StopBench => "inwestycje-lawki.csv",
        Investment.StopTactilePaving => "inwestycje-oznaczenia-dotykowe.csv",
        _ => $"inwestycje-{investment}.csv"
    };

    /// <summary>Nazwa rodzaju inwestycji do raportu.</summary>
    public static string Title(Investment investment) => investment switch
    {
        Investment.StopShelter => "Wiaty na przystankach",
        Investment.AccessibleToilet => "Dostosowanie toalet publicznych",
        Investment.StopBench => "Ławki na przystankach",
        Investment.StopTactilePaving => "Oznaczenia dotykowe na przystankach",
        _ => investment.ToString()
    };

    private static bool Yes(Place place, FeatureKey key) => place.Feature(key) is { State: FeatureState.Yes, IsDemoData: false };

    /// <summary>
    /// Obiekt, który można doposażyć: ma kategorię udogodnienia, ale samego udogodnienia nie (dane demonstracyjne się nie liczą).
    /// Toalety „dla klientów” (sklepy, lokale) nie są publiczne, więc nie są kandydatami do inwestycji miasta.
    /// </summary>
    public static bool IsCandidate(Place place, Investment investment) => investment switch
    {
        Investment.StopShelter or Investment.StopBench or Investment.StopTactilePaving =>
            place.Category == PlaceCategory.Stop && !Provides(place, investment),
        Investment.AccessibleToilet => place.Category == PlaceCategory.Toilet && !Provides(place, investment)
                                       && !place.Name.Contains("klient", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public static InvestmentPlan Plan(Investment investment, ResidentsGrid grid, IEnumerable<Place> places, DistrictMap? districts = null,
        int count = PickCount)
    {
        var seniorShare = grid.Districts.ToDictionary(d => d.Number, d => d.SeniorShare);
        var all = places as IReadOnlyCollection<Place> ?? places.ToList();
        // Zasięg dziś liczymy z tych samych obiektów, które planer zna, więc każdy rodzaj inwestycji ma spójny punkt wyjścia.
        var uncovered = Enumerable.Repeat(true, grid.Residents.Count).ToArray();
        foreach (var provider in all.Where(p => Provides(p, investment)))
            foreach (var i in CellsNear(grid, provider.Location))
                uncovered[i] = false;
        var withoutBefore = Enumerable.Range(0, uncovered.Length).Where(i => uncovered[i]).Sum(i => grid.Residents[i]);

        var stops = all.Where(p => p.Category == PlaceCategory.Stop).ToList();
        var candidates = all.Where(p => IsCandidate(p, investment)).ToList();
        // Dla każdego kandydata heksagony bez udogodnienia w promieniu; potem już tylko je przeliczamy.
        var reach = candidates.Select(c => CellsNear(grid, c.Location).Where(i => uncovered[i]).ToArray()).ToList();

        var picks = new List<InvestmentPick>();
        var cumulative = 0;
        var taken = new bool[candidates.Count];
        for (var round = 0; round < count; round++)
        {
            int best = -1, bestGain = 0;
            for (var c = 0; c < candidates.Count; c++)
            {
                if (taken[c])
                    continue;
                var gain = 0;
                foreach (var i in reach[c])
                    if (uncovered[i])
                        gain += grid.Residents[i];
                // Przy równym zysku decyduje identyfikator, żeby wynik był zawsze ten sam.
                if (gain > bestGain || gain == bestGain && gain > 0 && best >= 0 && string.CompareOrdinal(candidates[c].Id, candidates[best].Id) < 0)
                    (best, bestGain) = (c, gain);
            }
            if (best < 0)
                break;

            taken[best] = true;
            var seniors = 0.0;
            foreach (var i in reach[best])
            {
                if (!uncovered[i])
                    continue;
                uncovered[i] = false;
                seniors += grid.Residents[i] * seniorShare.GetValueOrDefault(grid.District[i]);
            }
            cumulative += bestGain;
            var place = candidates[best];
            var nearStop = place.Category == PlaceCategory.Stop ? null : stops
                .Select(s => (s.Name, Distance: s.Location.DistanceTo(place.Location)))
                .Where(x => x.Distance <= ResidentsReach.RadiusM)
                .OrderBy(x => x.Distance)
                .Select(x => x.Name)
                .FirstOrDefault();
            picks.Add(new InvestmentPick(place, bestGain, (int)Math.Round(seniors), cumulative, districts?.Find(place.Location)?.Name, nearStop));
        }

        return new InvestmentPlan(investment, candidates.Count, grid.Residents.Sum(), withoutBefore, picks);
    }

    private static IEnumerable<int> CellsNear(ResidentsGrid grid, GeoPoint point)
    {
        const double radius = ResidentsReach.RadiusM;
        var latDeg = radius / 111_320.0;
        var lonDeg = radius / (111_320.0 * Math.Cos(point.Lat * Math.PI / 180));
        for (var i = 0; i < grid.Residents.Count; i++)
            if (Math.Abs(grid.Lat[i] - point.Lat) <= latDeg && Math.Abs(grid.Lon[i] - point.Lon) <= lonDeg &&
                new GeoPoint(grid.Lat[i], grid.Lon[i]).DistanceTo(point) <= radius)
                yield return i;
    }
}
