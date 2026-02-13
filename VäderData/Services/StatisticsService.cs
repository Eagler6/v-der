using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VäderData.Models;

namespace VäderData.Services
{
    // Pass precomputed summaries (daily/monthly averages and mold-risk scores) between the statistics logic and the UI/file writer
    internal record DayTemp(DateTime Date, double AvgTempOutside, double AvgTempInside);
    internal record MonthAggregate(DateTime Date, double AvgTempOutside, double AvgTempInside, double AvgHumOutside, double AvgHumInside);
    internal record MoldRiskEntry(DateTime Date, double AvgTempOutside, double AvgHumOutside, double AvgTempInside, double AvgHumInside, int RiskOutside, int RiskInside);

    internal static class StatisticsService
    {
        // Per-day temperature averages
        internal static List<DayTemp> ComputePerDayTemps(List<WeatherData> list)
        {
            return list
                .GroupBy(d => d.Date.Date)
                .Select(g => new DayTemp(g.Key, g.Average(x => x.TemperatureOutside), g.Average(x => x.TemperatureInside)))
                .ToList();
        }

        // Per-month aggregates used in many outputs / file writing
        internal static List<MonthAggregate> ComputePerMonthAggregates(List<WeatherData> list)
        {
            return list
                .GroupBy(d => new { d.Date.Year, d.Date.Month })
                .Select(g => new MonthAggregate(
                    new DateTime(g.Key.Year, g.Key.Month, 1),
                    g.Average(x => x.TemperatureOutside),
                    g.Average(x => x.TemperatureInside),
                    g.Average(x => (double)x.HumidityOutside),
                    g.Average(x => (double)x.HumidityInside)
                ))
                .OrderBy(x => x.Date)
                .ToList();
        }

        // Mold risk entries by day
        internal static List<MoldRiskEntry> ComputeMoldRiskByDay(List<WeatherData> list)
        {
            var byDay = list
                .GroupBy(d => d.Date.Date)
                .Select(g =>
                {
                    var avgTempOutside = g.Average(x => x.TemperatureOutside);
                    var avgHumOutside = g.Average(x => x.HumidityOutside);
                    var avgTempInside = g.Average(x => x.TemperatureInside);
                    var avgHumInside = g.Average(x => x.HumidityInside);

                    var riskOutside = MoldCalculator.CalculateMoldRisk(avgTempOutside, avgHumOutside);
                    var riskInside = MoldCalculator.CalculateMoldRisk(avgTempInside, avgHumInside);

                    return new MoldRiskEntry(g.Key, avgTempOutside, avgHumOutside, avgTempInside, avgHumInside, riskOutside, riskInside);
                })
                .ToList();

            return byDay;
        }

        // Mold risk entries by month
        internal static List<MoldRiskEntry> ComputeMoldRiskByMonth(List<WeatherData> list)
        {
            var byMonth = list
                .GroupBy(d => new { d.Date.Year, d.Date.Month })
                .Select(g =>
                {
                    var avgTempOutside = g.Average(x => x.TemperatureOutside);
                    var avgHumOutside = g.Average(x => x.HumidityOutside);
                    var avgTempInside = g.Average(x => x.TemperatureInside);
                    var avgHumInside = g.Average(x => x.HumidityInside);

                    var riskOutside = MoldCalculator.CalculateMoldRisk(avgTempOutside, avgHumOutside);
                    var riskInside = MoldCalculator.CalculateMoldRisk(avgTempInside, avgHumInside);

                    return new MoldRiskEntry(new DateTime(g.Key.Year, g.Key.Month, 1), avgTempOutside, avgHumOutside, avgTempInside, avgHumInside, riskOutside, riskInside);
                })
                .ToList();

            return byMonth;
        }

        // Season detection logic
        internal static ((DateTime? arrival, int bestLen, DateTime bestStart) autumn, (DateTime? arrival, int bestLen, DateTime bestStart) winter) ComputeSeasonArrivals(List<WeatherData> list)
        {
            var dailyOutsideMeans = list
                .GroupBy(d => d.Date.Date)
                .Select(g =>
                {
                    var outsideValues = g.Where(x =>
                        (!string.IsNullOrEmpty(x.Location) && x.Location.ToLowerInvariant().Contains("ute")) ||
                        (!string.IsNullOrEmpty(x.Location) && x.Location.ToLowerInvariant().Contains("utomhus")) ||
                        Math.Abs(x.TemperatureOutside) > double.Epsilon
                    ).Select(x => x.TemperatureOutside).ToList();

                    return new
                    {
                        Date = g.Key,
                        HasOutside = outsideValues.Any(),
                        MeanOutside = outsideValues.Any() ? (double?)outsideValues.Average() : null
                    };
                })
                .OrderBy(x => x.Date)
                .ToList();

            (DateTime? arrival, int bestLen, DateTime bestStart) FindArrival(DateTime searchStart, DateTime searchEnd, Func<double, bool> predicate)
            {
                var dateSet = dailyOutsideMeans.ToDictionary(x => x.Date, x => x);
                for (var day = searchStart.Date; day <= searchEnd.Date.AddDays(-4); day = day.AddDays(1))
                {
                    bool ok = true;
                    for (int i = 0; i < 5; i++)
                    {
                        var d = day.AddDays(i);
                        if (!dateSet.TryGetValue(d, out var entry) || !entry.HasOutside || entry.MeanOutside == null || !predicate(entry.MeanOutside.Value))
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok) return (day, 5, day);
                }

                int bestLen = 0;
                DateTime bestStart = DateTime.MinValue;
                int currentLen = 0;
                DateTime currentStart = DateTime.MinValue;

                foreach (var e in dailyOutsideMeans)
                {
                    if (e.HasOutside && e.MeanOutside.HasValue && predicate(e.MeanOutside.Value))
                    {
                        if (currentLen == 0) currentStart = e.Date;
                        currentLen++;
                    }
                    else
                    {
                        if (currentLen > bestLen)
                        {
                            bestLen = currentLen;
                            bestStart = currentStart;
                        }
                        currentLen = 0;
                    }
                }
                if (currentLen > bestLen)
                {
                    bestLen = currentLen;
                    bestStart = currentStart;
                }

                return (null, bestLen, bestStart);
            }

            var autumnSearchStart = new DateTime(2016, 8, 1);
            var autumnSearchEnd = new DateTime(2017, 2, 14);
            Func<double, bool> autumnPredicate = mean => mean < 10.0;

            var autumnResult = FindArrival(autumnSearchStart, autumnSearchEnd, autumnPredicate);
            var winterResult = FindArrival(new DateTime(2016, 12, 1), new DateTime(2017, 2, 14), mean => mean <= 0.0);

            return (autumnResult, winterResult);
        }

        // Writes monthly averages + mold risk + season info to a file (replaces duplicate text in Menu)
        internal static void SaveMonthlyAveragesToFile(List<MonthAggregate> perMonthAggregates, (DateTime? arrival, int bestLen, DateTime bestStart) autumn, (DateTime? arrival, int bestLen, DateTime bestStart) winter)
        {
            try
            {
                string outFile = Path.Combine(AppContext.BaseDirectory, "monthly_averages.txt");

                using (var sw = new StreamWriter(outFile, false, System.Text.Encoding.UTF8))
                {
                    sw.WriteLine("Monthly averages (outside / inside) + Mold risk");
                    sw.WriteLine("==============================================");
                    foreach (var m in perMonthAggregates)
                    {
                        double avgTempOut = m.AvgTempOutside;
                        double avgHumOut = m.AvgHumOutside;
                        double avgTempIn = m.AvgTempInside;
                        double avgHumIn = m.AvgHumInside;

                        int moldRiskOut = MoldCalculator.CalculateMoldRisk(avgTempOut, avgHumOut);
                        int moldRiskIn = MoldCalculator.CalculateMoldRisk(avgTempIn, avgHumIn);

                        sw.WriteLine($"{m.Date:yyyy-MM}");
                        sw.WriteLine($"  AvgTempOutside: {avgTempOut:F2} °C");
                        sw.WriteLine($"  AvgTempInside : {avgTempIn:F2} °C");
                        sw.WriteLine($"  AvgHumOutside : {avgHumOut:F2} %");
                        sw.WriteLine($"  AvgHumInside  : {avgHumIn:F2} %");
                        sw.WriteLine($"  MoldRiskOutside: {moldRiskOut} (0-100)");
                        sw.WriteLine($"  MoldRiskInside : {moldRiskIn} (0-100)");
                        sw.WriteLine();
                    }

                    sw.WriteLine("Season dates (meteorological rules):");
                    // Autumn
                    if (autumn.arrival.HasValue)
                    {
                        sw.WriteLine($"  Meteorological autumn 2016 arrival: {autumn.arrival.Value:yyyy-MM-dd}");
                    }
                    else if (autumn.bestLen > 0)
                    {
                        sw.WriteLine($"  No full autumn arrival found; longest autumn-like run started {autumn.bestStart:yyyy-MM-dd} with length {autumn.bestLen} days (needs 5)."
                        );
                    }
                    else
                    {
                        sw.WriteLine("  No autumn-like data available to determine arrival.");
                    }

                    // Winter
                    if (winter.arrival.HasValue)
                    {
                        sw.WriteLine($"  Meteorological winter 2016/17 arrival: {winter.arrival.Value:yyyy-MM-dd}");
                    }
                    else if (winter.bestLen > 0)
                    {
                        sw.WriteLine($"  No full winter arrival found; longest winter-like run started {winter.bestStart:yyyy-MM-dd} with length {winter.bestLen} days (needs 5)."
                        );
                    }
                    else
                    {
                        sw.WriteLine("  No winter-like data available to determine arrival.");
                    }

                    sw.WriteLine();
                    sw.WriteLine("Mold risk algorithm (summary):");
                    sw.WriteLine("  - Humidity score (0..70):");
                    sw.WriteLine("      <70% => 0, 70-74 => 10, 75-79 => 30, 80-84 => 50, 85-89 => 60, >=90 => 70");
                    sw.WriteLine("  - Temperature score (0..30):");
                    sw.WriteLine("      <0°C => 0, 0-9 => 10, 10-19 => 20, 20-30 => 30, 31-40 => 15, >40 => 5");
                    sw.WriteLine("  - Final mold risk = humidityScore + temperatureScore (0..100)");
                    sw.WriteLine();
                }

            }
            catch
            {
                // keep silent on failures
            }
        }

        // Append season lines only (used for calls that want to write season lines without rewriting file)
        internal static void AppendSeasonLinesToFile((DateTime? arrival, int bestLen, DateTime bestStart) autumn, (DateTime? arrival, int bestLen, DateTime bestStart) winter)
        {
            try
            {
                string outFile = Path.Combine(AppContext.BaseDirectory, "monthly_averages.txt");

                using (var sw = new StreamWriter(outFile, true, System.Text.Encoding.UTF8))
                {
                    sw.WriteLine("Season dates (meteorological rules):");
                    if (autumn.arrival.HasValue)
                    {
                        sw.WriteLine($"  Meteorological autumn 2016 arrival: {autumn.arrival.Value:yyyy-MM-dd}");
                    }
                    else if (autumn.bestLen > 0)
                    {
                        sw.WriteLine($"  No full autumn arrival found; longest autumn-like run started {autumn.bestStart:yyyy-MM-dd} with length {autumn.bestLen} days (needs 5)."
                        );
                    }
                    else
                    {
                        sw.WriteLine("  No autumn-like data available to determine arrival.");
                    }

                    if (winter.arrival.HasValue)
                    {
                        sw.WriteLine($"  Meteorological winter 2016/17 arrival: {winter.arrival.Value:yyyy-MM-dd}");
                    }
                    else if (winter.bestLen > 0)
                    {
                        sw.WriteLine($"  No full winter arrival found; longest winter-like run started {winter.bestStart:yyyy-MM-dd} with length {winter.bestLen} days (needs 5)."
                        );
                    }
                    else
                    {
                        sw.WriteLine("  No winter-like data available to determine arrival.");
                    }

                    sw.WriteLine();
                }
            }
            catch
            {
                // silently ignore file write failures
            }
        }
    }
}
