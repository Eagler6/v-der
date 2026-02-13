using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static VäderData.MoldCalculator;

namespace VäderData
{
    internal class MoldCalculator
    {
        public static int CalculateMoldRisk(double temperature, double humidity)
        {
            int humidityScore = 0;
            int temperatureScore = 0;

            // Fuktdel (0–70 poäng)
            if (humidity < 70)
                humidityScore = 0;
            else if (humidity < 75)
                humidityScore = 10;
            else if (humidity < 80)
                humidityScore = 30;
            else if (humidity < 85)
                humidityScore = 50;
            else if (humidity < 90)
                humidityScore = 60;
            else
                humidityScore = 70;

            // Temperaturdel (0–30 poäng)
            if (temperature < 0)
                temperatureScore = 0;
            else if (temperature < 10)
                temperatureScore = 10;
            else if (temperature < 20)
                temperatureScore = 20;
            else if (temperature <= 30)
                temperatureScore = 30;
            else if (temperature <= 40)
                temperatureScore = 15;
            else
                temperatureScore = 5;

            return humidityScore + temperatureScore;
        }
    }
}
