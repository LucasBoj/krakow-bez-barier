namespace Domain.Places;

/// <summary>Udogodnienia, których zasięg liczymy dla mieszkańców. Kolejność to numer bitu w <see cref="ResidentsGrid.Reach"/>.</summary>
public enum Amenity
{
    Stop,
    ShelteredStop,
    Bench,
    Toilet,
    AccessibleToilet,
    DisabledParking
}

/// <summary>Stali mieszkańcy dzielnicy z ewidencji ludności (MSIP).</summary>
/// <param name="PostWorkingAge">W wieku poprodukcyjnym: kobiety 60+, mężczyźni 65+ (tak grupuje ewidencja).</param>
public sealed record DistrictResidents(int Number, string Name, int Total, int PostWorkingAge, int Age80Plus)
{
    public double SeniorShare => Total == 0 ? 0 : (double)PostWorkingAge / Total;

    public double Age80Share => Total == 0 ? 0 : (double)Age80Plus / Total;
}

/// <summary>
/// Siatka heksagonów 100 m ze stałymi zameldowaniami (MSIP), policzona przy imporcie: dla każdego heksagonu dzielnica
/// i maska udogodnień w promieniu <see cref="RadiusM"/> od jego środka. Listy są równoległe (indeks = heksagon).
/// </summary>
/// <param name="District">Numer dzielnicy, w której leży środek heksagonu; 0 poza dzielnicami.</param>
/// <param name="Reach">Bit <c>1 &lt;&lt; (int)Amenity</c> ustawiony, gdy udogodnienie jest w promieniu.</param>
public sealed record ResidentsGrid(
    string Source, DateOnly GridDate, DateOnly DistrictsDate, double RadiusM,
    IReadOnlyList<double> Lat, IReadOnlyList<double> Lon, IReadOnlyList<int> Residents, IReadOnlyList<int> District, IReadOnlyList<int> Reach,
    IReadOnlyList<DistrictResidents> Districts);

/// <param name="WithAccess">Mieszkańców, którzy mają udogodnienie w promieniu.</param>
public sealed record AmenityReach(Amenity Amenity, int Residents, int WithAccess)
{
    public int Without => Residents - WithAccess;

    public double Share => Residents == 0 ? 0 : (double)WithAccess / Residents;
}

/// <param name="Residents">Stałe zameldowania z siatki w dzielnicy.</param>
/// <param name="Registry">Wiek mieszkańców z ewidencji dzielnic; null, gdy brak wiersza dla dzielnicy.</param>
public sealed record DistrictReach(int Number, string Roman, string Name, int Residents, DistrictResidents? Registry, IReadOnlyList<AmenityReach> Amenities)
{
    public AmenityReach Of(Amenity amenity) => Amenities.First(a => a.Amenity == amenity);
}

/// <param name="R">Współczynnik korelacji Pearsona między udziałem seniorów w dzielnicy a udziałem mieszkańców z udogodnieniem; null przy braku zmienności.</param>
/// <param name="SeniorsWithoutEstimate">Szacunek: mieszkańcy dzielnicy bez udogodnienia × udział seniorów w dzielnicy, zsumowane po dzielnicach.</param>
public sealed record AmenityCorrelation(Amenity Amenity, double? R, int SeniorsWithoutEstimate);

public sealed record ResidentsReport(
    int Residents, DateOnly GridDate, DateOnly DistrictsDate, double RadiusM, IReadOnlyList<AmenityReach> Total, IReadOnlyList<DistrictReach> Districts,
    IReadOnlyList<AmenityCorrelation> Correlations, int RegistryTotal, int RegistrySeniors, int Registry80Plus)
{
    public AmenityReach Of(Amenity amenity) => Total.First(a => a.Amenity == amenity);
}

/// <summary>Ilu mieszkańców ma udogodnienia w zasięgu krótkiego spaceru i jak to się ma do wieku mieszkańców dzielnic.</summary>
public static class ResidentsReach
{
    /// <summary>Promień w linii prostej od środka heksagonu.</summary>
    public const double RadiusM = 300;

    public const string Rule =
        "Mieszkańcy to stałe zameldowania w heksagonach 100 m z ewidencji ludności (MSIP); osoby zameldowane czasowo i niezameldowane nie są ujęte. " +
        "Udogodnienie jest „w zasięgu”, gdy leży do 300 m w linii prostej od środka heksagonu. Seniorzy to osoby w wieku poprodukcyjnym " +
        "(kobiety 60+, mężczyźni 65+) z ewidencji dzielnic. Ławki pochodzą z OpenStreetMap, więc braki w mapowaniu zaniżają ich zasięg.";

    public static readonly IReadOnlyList<Amenity> All = Enum.GetValues<Amenity>();

    public static string Label(Amenity amenity) => amenity switch
    {
        Amenity.Stop => "przystanek komunikacji miejskiej",
        Amenity.ShelteredStop => "przystanek z wiatą",
        Amenity.Bench => "ławka (także na przystanku)",
        Amenity.Toilet => "toaleta publiczna",
        Amenity.AccessibleToilet => "toaleta publiczna dostosowana",
        Amenity.DisabledParking => "miejsce postojowe dla osób z niepełnosprawnościami",
        _ => amenity.ToString()
    };

    /// <summary>Nazwa kolumny w eksporcie.</summary>
    public static string Slug(Amenity amenity) => amenity switch
    {
        Amenity.Stop => "przystanek",
        Amenity.ShelteredStop => "wiata",
        Amenity.Bench => "lawka",
        Amenity.Toilet => "toaleta",
        Amenity.AccessibleToilet => "toaleta_dostosowana",
        Amenity.DisabledParking => "koperta",
        _ => amenity.ToString().ToLowerInvariant()
    };

    /// <summary>Dane demonstracyjne się nie liczą: zasięg ma opisywać miasto, nie ścieżkę demo.</summary>
    public static bool Provides(Place place, Amenity amenity) => amenity switch
    {
        Amenity.Stop => place.Category == PlaceCategory.Stop,
        Amenity.ShelteredStop => place.Category == PlaceCategory.Stop && Yes(place, FeatureKey.StopShelter),
        Amenity.Bench => place.Category == PlaceCategory.Bench || Yes(place, FeatureKey.Benches),
        Amenity.Toilet => place.Category == PlaceCategory.Toilet,
        Amenity.AccessibleToilet => place.Category == PlaceCategory.Toilet && Yes(place, FeatureKey.AccessibleToilet),
        Amenity.DisabledParking => place.Category == PlaceCategory.DisabledParking,
        _ => false
    };

    private static bool Yes(Place place, FeatureKey key) => place.Feature(key) is { State: FeatureState.Yes, IsDemoData: false };

    /// <summary>Maska udogodnień w promieniu dla każdego punktu (środka heksagonu).</summary>
    public static int[] Compute(IReadOnlyList<GeoPoint> points, IEnumerable<Place> places, double radiusM = RadiusM)
    {
        var index = new PointIndex(radiusM);
        foreach (var place in places)
        {
            var mask = 0;
            foreach (var amenity in All)
                if (Provides(place, amenity))
                    mask |= 1 << (int)amenity;
            if (mask != 0)
                index.Add(place.Location, mask);
        }

        var result = new int[points.Count];
        for (var i = 0; i < points.Count; i++)
            result[i] = index.MaskNear(points[i], radiusM);
        return result;
    }

    /// <summary>Stałe zameldowania w heksagonach, których środek jest w promieniu od punktu.</summary>
    public static int ResidentsNear(ResidentsGrid grid, GeoPoint point, double radiusM = RadiusM)
    {
        var latDeg = radiusM / 111_320.0;
        var lonDeg = radiusM / (111_320.0 * Math.Cos(point.Lat * Math.PI / 180));
        var sum = 0;
        for (var i = 0; i < grid.Residents.Count; i++)
            if (Math.Abs(grid.Lat[i] - point.Lat) <= latDeg && Math.Abs(grid.Lon[i] - point.Lon) <= lonDeg &&
                new GeoPoint(grid.Lat[i], grid.Lon[i]).DistanceTo(point) <= radiusM)
                sum += grid.Residents[i];
        return sum;
    }

    public static ResidentsReport Build(ResidentsGrid grid, DistrictMap? districts)
    {
        var total = Reach(Enumerable.Range(0, grid.Residents.Count), grid);
        var registry = grid.Districts.ToDictionary(d => d.Number);
        var byDistrict = (districts?.Districts ?? [])
            .Select(d => new DistrictReach(d.Number, d.Roman, d.Name,
                Enumerable.Range(0, grid.Residents.Count).Where(i => grid.District[i] == d.Number).Sum(i => grid.Residents[i]),
                registry.GetValueOrDefault(d.Number),
                Reach(Enumerable.Range(0, grid.Residents.Count).Where(i => grid.District[i] == d.Number), grid)))
            .ToList();

        var withAge = byDistrict.Where(d => d.Registry is { Total: > 0 } && d.Residents > 0).ToList();
        var correlations = All.Select(a => new AmenityCorrelation(a,
                Pearson(withAge.Select(d => d.Registry!.SeniorShare).ToList(), withAge.Select(d => d.Of(a).Share).ToList()),
                (int)Math.Round(withAge.Sum(d => d.Of(a).Without * d.Registry!.SeniorShare))))
            .ToList();

        return new ResidentsReport(grid.Residents.Sum(), grid.GridDate, grid.DistrictsDate, grid.RadiusM, total, byDistrict, correlations,
            grid.Districts.Sum(d => d.Total), grid.Districts.Sum(d => d.PostWorkingAge), grid.Districts.Sum(d => d.Age80Plus));
    }

    private static IReadOnlyList<AmenityReach> Reach(IEnumerable<int> cells, ResidentsGrid grid)
    {
        var list = cells.ToList();
        var residents = list.Sum(i => grid.Residents[i]);
        return All.Select(a => new AmenityReach(a, residents, list.Where(i => (grid.Reach[i] & (1 << (int)a)) != 0).Sum(i => grid.Residents[i]))).ToList();
    }

    /// <summary>Współczynnik korelacji Pearsona; null dla mniej niż 3 par albo gdy jedna ze zmiennych jest stała.</summary>
    public static double? Pearson(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        if (xs.Count != ys.Count || xs.Count < 3)
            return null;
        double mx = xs.Average(), my = ys.Average(), sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < xs.Count; i++)
        {
            sxy += (xs[i] - mx) * (ys[i] - my);
            sxx += (xs[i] - mx) * (xs[i] - mx);
            syy += (ys[i] - my) * (ys[i] - my);
        }
        return sxx == 0 || syy == 0 ? null : sxy / Math.Sqrt(sxx * syy);
    }

    /// <summary>
    /// Kubełki o boku promienia w stopniach szerokości (w obu osiach). Na wschód i zachód kubełek jest węższy niż promień,
    /// więc przeszukujemy tyle kolumn, ile trzeba dla szerokości geograficznej punktu.
    /// </summary>
    private sealed class PointIndex(double cellM)
    {
        private readonly double _cellDeg = cellM / 111_320.0;
        private readonly Dictionary<(int, int), List<(GeoPoint Point, int Mask)>> _cells = [];

        private (int, int) Key(GeoPoint p) => ((int)Math.Floor(p.Lat / _cellDeg), (int)Math.Floor(p.Lon / _cellDeg));

        public void Add(GeoPoint point, int mask)
        {
            var key = Key(point);
            if (!_cells.TryGetValue(key, out var list))
                _cells[key] = list = [];
            list.Add((point, mask));
        }

        public int MaskNear(GeoPoint point, double radiusM)
        {
            var (row, col) = Key(point);
            var span = (int)Math.Ceiling(radiusM / cellM / Math.Cos(point.Lat * Math.PI / 180));
            var mask = 0;
            for (var r = row - 1; r <= row + 1; r++)
                for (var c = col - span; c <= col + span; c++)
                    if (_cells.TryGetValue((r, c), out var list))
                        foreach (var (p, m) in list)
                            if ((mask | m) != mask && p.DistanceTo(point) <= radiusM)
                                mask |= m;
            return mask;
        }
    }
}
