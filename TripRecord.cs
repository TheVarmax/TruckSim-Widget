using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ETSOverlay
{
    public class TripRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Version { get; set; } = 1;
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }

        // Basic fields (Free tier)
        public string Origin { get; set; } = "";
        public string Destination { get; set; } = "";
        public string CargoName { get; set; } = "";
        public float DistanceKm { get; set; }
        public long DurationTicks { get; set; }  // TimeSpan stored as ticks for JSON compat

        [JsonIgnore]
        public TimeSpan Duration
        {
            get => TimeSpan.FromTicks(DurationTicks);
            set => DurationTicks = value.Ticks;
        }

        // Extended fields (Supporter tier)
        public float AverageSpeedKmh { get; set; }
        public int MaxSpeedKmh { get; set; }
        public float AvgFuelConsumptionLPer100Km { get; set; }
        public float TotalFuelConsumedL { get; set; }
        public float TruckDamagePercent { get; set; }
        public float TrailerDamagePercent { get; set; }
        public float CargoDamagePercent { get; set; }
        public ulong Income { get; set; }
        public string TruckBrand { get; set; } = "";
        public string TruckName { get; set; } = "";

        // Metadata
        public string GameType { get; set; } = ""; // "ETS" or "ATS"

        // Future extensibility
        public Dictionary<string, string> Extra { get; set; } = new();
    }
}
