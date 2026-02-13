using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VäderData.Models
{
    internal class WeatherData
    {
        public DateTime Date { get; set; }
        public string Location { get; set; } 
        public double TemperatureOutside { get; set; }
        public double TemperatureInside { get; set; }
        public int HumidityOutside { get; set; }
        public int HumidityInside { get; set; }
        public double MoldRiskOutside { get; set; }
        public double MoldRiskInside { get; set; }
    }
}
