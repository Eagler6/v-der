using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using VäderData.Infrastructure;
using VäderData.Models;
using VäderData.Services;

namespace VäderData
{
    internal class Menu
    {
        private const string DataFile = "tempdata5-med fel.txt";

        public static string MenuStartPage()
        {
            Console.Clear();
            Console.WriteLine("Väderapp");
            Console.WriteLine("==========================");
            Console.WriteLine("Tryck 1 för att söka Datum");
            Console.WriteLine("Tryck 2 för Statistik");
            Console.WriteLine("Tryck 3 för att Avsluta");

            return Console.ReadLine();
        }

        public static string MenuSearchDate()
        {
            Console.Clear();
            Console.WriteLine("Enter a date to check per dayrecord(yyyy-mm-dd): ");
            string input = Console.ReadLine();
            var processor = new FileProcessor();
            List<WeatherData> WeatherList = processor.LoadWeatherFile(DataFile);

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("No input provided.");
                return input;
            }

            if (!DateTime.TryParseExact(input.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime selectedDate))
            {
                if (!DateTime.TryParse(input.Trim(), out selectedDate))
                {
                    Console.WriteLine("Invalid date format. Please use yyyy-mm-dd");
                    return input;
                }
            }

            var dailyRecords = WeatherList.Where(d => d.Date.Date == selectedDate.Date).ToList();

            if (!dailyRecords.Any())
            {
                Console.WriteLine("No data found for selected date.");
                Console.WriteLine($"Searched date: {selectedDate:yyyy-MM-dd}");
                Console.WriteLine($"Loaded records: {WeatherList.Count}");
                if (WeatherList.Any())
                {
                    var minDate = WeatherList.Min(d => d.Date);
                    var maxDate = WeatherList.Max(d => d.Date);
                    Console.WriteLine($"Data range: {minDate:yyyy-MM-dd} .. {maxDate:yyyy-MM-dd}");
                }
                Console.ReadKey();
                return input;
            }

            var result = dailyRecords
                .GroupBy(d => d.Date.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    AvgTemperatureOutside = g.Average(x => x.TemperatureOutside),
                    AvgTemperatureInside = g.Average(x => x.TemperatureInside),
                    AvgHumidityOutside = g.Average(x => x.HumidityOutside),
                    AvgHumidityInside = g.Average(x => x.HumidityInside)
                })
                .First();

            Console.WriteLine($"Date: {result.Date:yyyy-MM-dd}");
            Console.WriteLine($"Average Temperature Outside: {result.AvgTemperatureOutside:F2}");
            Console.WriteLine($"Average Temperature Inside:  {result.AvgTemperatureInside:F2}");
            Console.WriteLine($"Average Humidity Outside:    {result.AvgHumidityOutside:F2}");
            Console.WriteLine($"Average Humidity Inside:     {result.AvgHumidityInside:F2}");
            Console.ReadKey();
            return input;
        }

        public static void MenuStatisticsMenu()
        {
            Console.Clear();
            Console.WriteLine("Statistik - välj ett alternativ:");
            Console.WriteLine("1 - Statistik för temperatur");
            Console.WriteLine("2 - Statistik för mögelrisk");
            Console.WriteLine("3 - Tillbaka");
            string input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    MenuStatisticsTemperature();
                    break;
                case "2":
                    MenuStatisticsMoldRisk();
                    break;
                default:
                    break;
            }
        }

        public static void MenuStatisticsTemperature()
        {
            Console.Clear();
            var processor = new FileProcessor();
            List<WeatherData> WeatherList = processor.LoadWeatherFile(DataFile);

            Console.WriteLine("\ndata for warmest to coldest (temperature stats)");

            var perDayTemps = StatisticsService.ComputePerDayTemps(WeatherList);

            Console.WriteLine("Warmest per day (ute högst först):");
            foreach (var day in perDayTemps.OrderByDescending(x => x.AvgTempOutside))
            {
                Console.WriteLine($"{day.Date:yyyy-MM-dd} - Ute: {day.AvgTempOutside:F2}°C, Inne: {day.AvgTempInside:F2}°C");
            }

            Console.WriteLine("\nWarmest per day (inne högst först):");
            foreach (var day in perDayTemps.OrderByDescending(x => x.AvgTempInside))
            {
                Console.WriteLine($"{day.Date:yyyy-MM-dd} - Inne: {day.AvgTempInside:F2}°C, Ute: {day.AvgTempOutside:F2}°C");
            }

            var perMonthTemps = StatisticsService.ComputePerMonthAggregates(WeatherList);

            Console.WriteLine("\nWarmest per month (ute högst först):");
            foreach (var m in perMonthTemps.OrderByDescending(x => x.AvgTempOutside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM} - Ute: {m.AvgTempOutside:F2}°C, Inne: {m.AvgTempInside:F2}°C");
            }

            Console.WriteLine("\nWarmest per month (inne högst först):");
            foreach (var m in perMonthTemps.OrderByDescending(x => x.AvgTempInside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM} - Inne: {m.AvgTempInside:F2}°C, Ute: {m.AvgTempOutside:F2}°C");
            }

            var seasonResults = StatisticsService.ComputeSeasonArrivals(WeatherList);
            StatisticsService.AppendSeasonLinesToFile(seasonResults.autumn, seasonResults.winter);

            Console.ReadKey();
        }

        public static void MenuStatisticsMoldRisk()
        {
            Console.Clear();
            var processor = new FileProcessor();
            List<WeatherData> WeatherList = processor.LoadWeatherFile(DataFile);

            var perMonthAggregates = StatisticsService.ComputePerMonthAggregates(WeatherList);

            try
            {
                StatisticsService.SaveMonthlyAveragesToFile(perMonthAggregates, StatisticsService.ComputeSeasonArrivals(WeatherList).autumn, StatisticsService.ComputeSeasonArrivals(WeatherList).winter);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save monthly averages: {ex.Message}");
            }

            var moldRiskByDay = StatisticsService.ComputeMoldRiskByDay(WeatherList);

            Console.WriteLine("\nMögelrisk per dag (ute högst först):");
            foreach (var m in moldRiskByDay.OrderByDescending(x => x.RiskOutside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM-dd} - Ute: {m.RiskOutside} (T: {m.AvgTempOutside:F2}°C, H: {m.AvgHumOutside:F2}%) - Inne: {m.RiskInside} (T: {m.AvgTempInside:F2}°C, H: {m.AvgHumInside:F2}%)");
            }

            Console.WriteLine("\nMögelrisk per dag (inne högst först):");
            foreach (var m in moldRiskByDay.OrderByDescending(x => x.RiskInside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM-dd} - Inne: {m.RiskInside} (T: {m.AvgTempInside:F2}°C, H: {m.AvgHumInside:F2}%) - Ute: {m.RiskOutside}");
            }

            var moldRiskByMonth = StatisticsService.ComputeMoldRiskByMonth(WeatherList);

            Console.WriteLine("\nMögelrisk per månad (ute högst först):");
            foreach (var m in moldRiskByMonth.OrderByDescending(x => x.RiskOutside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM} - Ute: {m.RiskOutside} (T: {m.AvgTempOutside:F2}°C, H: {m.AvgHumOutside:F2}%) - Inne: {m.RiskInside}");
            }

            Console.WriteLine("\nMögelrisk per månad (inne högst först):");
            foreach (var m in moldRiskByMonth.OrderByDescending(x => x.RiskInside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM} - Inne: {m.RiskInside} (T: {m.AvgTempInside:F2}°C, H: {m.AvgHumInside:F2}%) - Ute: {m.RiskOutside}");
            }

            Console.ReadKey();
        }

        // Keep the legacy MenuStatistics for compatibility (calls refactored functions)
        public static void MenuStatistics()
        {
            Console.Clear();
            var processor = new FileProcessor();
            List<WeatherData> WeatherList = processor.LoadWeatherFile(DataFile);
            Console.WriteLine("\ndata for warmest to coldest");

            var perDayTemps = StatisticsService.ComputePerDayTemps(WeatherList);

            Console.WriteLine("Warmest per day (ute högst först):");
            foreach (var day in perDayTemps.OrderByDescending(x => x.AvgTempOutside))
            {
                Console.WriteLine($"{day.Date:yyyy-MM-dd} - Ute: {day.AvgTempOutside:F2}°C, Inne: {day.AvgTempInside:F2}°C");
            }

            Console.WriteLine("\nWarmest per day (inne högst först):");
            foreach (var day in perDayTemps.OrderByDescending(x => x.AvgTempInside))
            {
                Console.WriteLine($"{day.Date:yyyy-MM-dd} - Inne: {day.AvgTempInside:F2}°C, Ute: {day.AvgTempOutside:F2}°C");
            }

            var perMonthAggregates = StatisticsService.ComputePerMonthAggregates(WeatherList);

            Console.WriteLine("\nWarmest per month (ute högst först):");
            foreach (var m in perMonthAggregates.OrderByDescending(x => x.AvgTempOutside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM} - Ute: {m.AvgTempOutside:F2}°C, Inne: {m.AvgTempInside:F2}°C");
            }

            Console.WriteLine("\nDriest per month (ute torrast först):");
            foreach (var m in perMonthAggregates.OrderBy(x => x.AvgHumOutside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM} - Ute: {m.AvgHumOutside:F2}%, Inne: {m.AvgHumInside:F2}%");
            }

            var seasonResults = StatisticsService.ComputeSeasonArrivals(WeatherList);
            StatisticsService.SaveMonthlyAveragesToFile(perMonthAggregates, seasonResults.autumn, seasonResults.winter);

            var moldRiskByDay = StatisticsService.ComputeMoldRiskByDay(WeatherList);

            Console.WriteLine("\nMögelrisk per dag (ute högst först):");
            foreach (var m in moldRiskByDay.OrderByDescending(x => x.RiskOutside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM-dd} - Ute: {m.RiskOutside} (T: {m.AvgTempOutside:F2}°C, H: {m.AvgHumOutside:F2}%) - Inne: {m.RiskInside} (T: {m.AvgTempInside:F2}°C, H: {m.AvgHumInside:F2}%)");
            }

            Console.WriteLine("\nMögelrisk per dag (inne högst först):");
            foreach (var m in moldRiskByDay.OrderByDescending(x => x.RiskInside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM-dd} - Inne: {m.RiskInside} (T: {m.AvgTempInside:F2}°C, H: {m.AvgHumInside:F2}%) - Ute: {m.RiskOutside}");
            }

            var moldRiskByMonth = StatisticsService.ComputeMoldRiskByMonth(WeatherList);

            Console.WriteLine("\nMögelrisk per månad (ute högst först):");
            foreach (var m in moldRiskByMonth.OrderByDescending(x => x.RiskOutside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM} - Ute: {m.RiskOutside} (T: {m.AvgTempOutside:F2}°C, H: {m.AvgHumOutside:F2}%) - Inne: {m.RiskInside}");
            }

            Console.WriteLine("\nMögelrisk per månad (inne högst först):");
            foreach (var m in moldRiskByMonth.OrderByDescending(x => x.RiskInside))
            {
                Console.WriteLine($"{m.Date:yyyy-MM} - Inne: {m.RiskInside} (T: {m.AvgTempInside:F2}°C, H: {m.AvgHumInside:F2}%) - Ute: {m.RiskOutside}");
            }

            Console.ReadKey();
        }
    }
}
