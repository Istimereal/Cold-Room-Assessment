using System.Globalization;
using ColdRoomAssessment.Application.Analysis;
using ColdRoomAssessment.Application.Coverage;

namespace ColdRoomAssessment.Application.Investigation;

public sealed class EvidenceCatalog
{
    private readonly HashSet<string> _ids;

    private EvidenceCatalog(
        Guid facilityId,
        string facilityName,
        DateTime from,
        DateTime to,
        IReadOnlyList<EvidenceItem> items)
    {
        FacilityId = facilityId;
        FacilityName = facilityName;
        From = from;
        To = to;
        Items = items;
        _ids = new HashSet<string>(items.Count, StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!_ids.Add(item.Id))
            {
                throw new InvalidOperationException("The evidence catalog contains a duplicate id.");
            }
        }
    }

    public Guid FacilityId { get; }

    public string FacilityName { get; }

    public DateTime From { get; }

    public DateTime To { get; }

    public IReadOnlyList<EvidenceItem> Items { get; }

    public bool Contains(string? id) => id is not null && _ids.Contains(id);

    public static EvidenceCatalog FromAnalysis(FacilityAnalysis analysis)
    {
        var items = new List<EvidenceItem>();
        foreach (var equipment in analysis.Equipment)
        {
            AddKind(items, equipment, temperature: true, equipment.Temperature);
            AddKind(items, equipment, temperature: false, equipment.RelativeHumidity);
        }

        return new EvidenceCatalog(
            analysis.FacilityId,
            analysis.FacilityName,
            analysis.From,
            analysis.To,
            items);
    }

    private static void AddKind(
        List<EvidenceItem> items,
        EquipmentAnalysis equipment,
        bool temperature,
        KindAnalysis kind)
    {
        var measurementKind = temperature ? "Temperature" : "RelativeHumidity";
        var slug = temperature ? "temperature" : "relative-humidity";
        Add(items, equipment, measurementKind, slug, "coverage", DecimalText(equipment.CoveragePercent), null);
        Add(items, equipment, measurementKind, slug, "missing-count", Integer(equipment.MissingExpectedSampleCount), null);
        Add(items, equipment, measurementKind, slug, "ambiguous-count", Integer(equipment.AmbiguousSampleCount), null);
        Add(items, equipment, measurementKind, slug, "count", Integer(kind.Count), null);
        Add(items, equipment, measurementKind, slug, "minimum", Number(kind.Min), Timestamp(kind.MinAt));
        Add(items, equipment, measurementKind, slug, "maximum", Number(kind.Max), Timestamp(kind.MaxAt));
        Add(items, equipment, measurementKind, slug, "mean", Number(kind.Mean), null);
        Add(items, equipment, measurementKind, slug, "median", Number(kind.Median), null);
        Add(items, equipment, measurementKind, slug, "population-standard-deviation", Number(kind.PopulationStdDev), null);
        Add(items, equipment, measurementKind, slug, "first", Number(kind.First), Timestamp(kind.FirstAt));
        Add(items, equipment, measurementKind, slug, "last", Number(kind.Last), Timestamp(kind.LastAt));
        Add(items, equipment, measurementKind, slug, "change", Number(kind.Change), null);
        Add(items, equipment, measurementKind, slug, "duration-seconds", Number(kind.DurationSeconds), null);

        var peaks = 0;
        var dips = 0;
        foreach (var seriesEvent in kind.Events)
        {
            if (seriesEvent.Kind == SeriesEventKind.Peak)
            {
                peaks++;
                Add(
                    items,
                    equipment,
                    measurementKind,
                    slug,
                    $"peak:{peaks}",
                    Number(seriesEvent.Value),
                    Timestamp(seriesEvent.Timestamp));
            }
            else
            {
                dips++;
                Add(
                    items,
                    equipment,
                    measurementKind,
                    slug,
                    $"dip:{dips}",
                    Number(seriesEvent.Value),
                    Timestamp(seriesEvent.Timestamp));
            }
        }
    }

    private static void Add(
        List<EvidenceItem> items,
        EquipmentAnalysis equipment,
        string measurementKind,
        string slug,
        string fact,
        string? value,
        string? timestamp)
    {
        items.Add(new EvidenceItem(
            $"eq:{equipment.EquipmentId:D}:{slug}:{fact}",
            equipment.EquipmentId,
            equipment.EquipmentName,
            measurementKind,
            fact,
            value,
            timestamp));
    }

    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string DecimalText(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? Number(double? value) =>
        value?.ToString("G17", CultureInfo.InvariantCulture);

    private static string? Timestamp(DateTime? value) =>
        value?.ToString(AnalysisPeriodService.TimestampFormat, CultureInfo.InvariantCulture);
}

public sealed record EvidenceItem(
    string Id,
    Guid EquipmentId,
    string EquipmentName,
    string MeasurementKind,
    string Fact,
    string? Value,
    string? Timestamp);
