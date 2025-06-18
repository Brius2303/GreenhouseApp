using System;
using System.Collections.Generic;

namespace GreenhouseApp
{
    public class Greenhouse
    {
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; } = 0;
    }

    public class Device
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; 
        public ushort Register { get; set; }
        public bool IsOn { get; set; }
        public bool IsAuto { get; set; }
        public string GreenhouseName { get; set; } = string.Empty;

        public double? TemperatureTrigger { get; set; }
        public double? SoilMoistureTrigger { get; set; } 
        public int? DaylightHours { get; set; } 
        public DateTime? LightOnTime { get; set; } 

    }

    public class SensorData
    {
        public DateTime Timestamp { get; set; }
        public double Temperature { get; set; }
        public int Humidity { get; set; }
        public double SoilMoisture { get; set; }
        public int CO2 { get; set; }
        public bool IsConnected { get; set; }
        public string GreenhouseName { get; set; } = string.Empty;
    }

}
