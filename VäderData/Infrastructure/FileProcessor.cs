using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VäderData.Models;

namespace VäderData.Infrastructure
{
    internal class FileProcessor
    {
        public List<WeatherData> LoadWeatherFile(string filePath)
        {
            var allData = new List<WeatherData>();

            // Named groups: date, loc, temp, hum
            string pattern = @"^(?<date>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2})\s*,\s*(?<loc>[^,]+)\s*,\s*(?<temp>-?\d+(?:[.,]\d+)?)\s*,\s*(?<hum>\d+)\s*$";
            var rx = new Regex(pattern, RegexOptions.Compiled);

            string firstLine = File.ReadLines(filePath).FirstOrDefault();

            foreach (string line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var match = rx.Match(line);
                if (!match.Success)
                    continue; // skip lines that don't match expected format

                string dateText = match.Groups["date"].Value;
                if (!DateTime.TryParseExact(dateText, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                {
                    // fallback to general parse if exact fails
                    if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                        continue;
                }

                // preserve original filtering behaviour
                if ((date.Year == 2016 && date.Month == 5) || (date.Year == 2017 && date.Month == 1))
                    continue;

                string loc = match.Groups["loc"].Value.Trim();
                string tempText = match.Groups["temp"].Value.Trim().Replace("\t", "").Replace(",", ".");
                if (!double.TryParse(tempText, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double temp))
                    continue;

                string humText = match.Groups["hum"].Value.Trim();
                if (!int.TryParse(humText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hum))
                    continue;

                // Assign values only to the correct side based on Swedish location tokens.
                var wd = new WeatherData
                {
                    Date = date,
                    Location = loc // will override below for known tokens
                };

                string locNorm = loc.ToLowerInvariant();
                if (locNorm.Contains("ute") || locNorm.Contains("utomhus"))
                {
                    // outside -> set outside fields, Location must be "ute"
                    wd.Location = "ute";
                    wd.TemperatureOutside = temp;
                    wd.HumidityOutside = hum;
                }
                else if (locNorm.Contains("inne") || locNorm.Contains("inomhus"))
                {
                    // inside -> set inside fields, Location must be "Inne"
                    wd.Location = "Inne";
                    wd.TemperatureInside = temp;
                    wd.HumidityInside = hum;
                }
                else
                {
                    wd.Location = loc;
                    wd.TemperatureOutside = temp;
                    wd.HumidityOutside = hum;
                }

                allData.Add(wd);
            }

            return allData;
        }
    }
}
