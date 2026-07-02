using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ETSOverlay
{
    public sealed class TripLogbookService
    {
        private static readonly Lazy<TripLogbookService> _instance = new(() => new TripLogbookService());
        public static TripLogbookService Instance => _instance.Value;

        private readonly string _logbookDir;

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private TripLogbookService()
        {
            _logbookDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TruckSimWidget",
                "logbook");

            try
            {
                Directory.CreateDirectory(_logbookDir);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TripLogbook] Failed to create logbook directory: {ex.Message}");
            }
        }

        public void SaveTrip(TripRecord trip)
        {
            try
            {
                // Prevent duplicate saves (same trip within 2 minutes)
                var recentTrips = LoadTrips(5);
                if (recentTrips.Any(t => 
                    t.Origin == trip.Origin && 
                    t.Destination == trip.Destination &&
                    Math.Abs(t.DistanceKm - trip.DistanceKm) < 1f &&
                    (trip.EndTimeUtc - t.EndTimeUtc).TotalMinutes < 2))
                {
                    Debug.WriteLine("[TripLogbook] Duplicate trip detected, skipping save.");
                    return;
                }

                var filePath = Path.Combine(_logbookDir, $"{trip.Id}.json");
                var json = JsonSerializer.Serialize(trip, WriteOptions);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TripLogbook] Failed to save trip {trip.Id}: {ex.Message}");
            }
        }

        public List<TripRecord> LoadTrips(int? limit = null)
        {
            var trips = new List<TripRecord>();

            try
            {
                if (!Directory.Exists(_logbookDir))
                    return trips;

                var files = Directory.GetFiles(_logbookDir, "*.json");

                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file, Encoding.UTF8);
                        var trip = JsonSerializer.Deserialize<TripRecord>(json, ReadOptions);
                        if (trip != null)
                            trips.Add(trip);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TripLogbook] Failed to load trip from {Path.GetFileName(file)}: {ex.Message}");
                    }
                }

                trips.Sort((a, b) => b.EndTimeUtc.CompareTo(a.EndTimeUtc));

                if (limit.HasValue && limit.Value > 0)
                    trips = trips.Take(limit.Value).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TripLogbook] Failed to load trips: {ex.Message}");
            }

            return trips;
        }

        public void DeleteTrip(string id)
        {
            try
            {
                var filePath = Path.Combine(_logbookDir, $"{id}.json");
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TripLogbook] Failed to delete trip {id}: {ex.Message}");
            }
        }

        public void ExportToCsv(List<TripRecord> trips, string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Origin,Destination,Cargo,DistanceKm,Duration,Start,End,AvgSpeed,MaxSpeed,AvgFuelConsumption,TotalFuelConsumed,TruckDamage,TrailerDamage,CargoDamage,Income,Truck,Game");

                foreach (var t in trips)
                {
                    sb.AppendLine(string.Join(",",
                        CsvEscape(t.Origin),
                        CsvEscape(t.Destination),
                        CsvEscape(t.CargoName),
                        t.DistanceKm.ToString("F1"),
                        t.Duration.ToString(@"hh\:mm\:ss"),
                        t.StartTimeUtc.ToString("o"),
                        t.EndTimeUtc.ToString("o"),
                        t.AverageSpeedKmh.ToString("F1"),
                        t.MaxSpeedKmh,
                        t.AvgFuelConsumptionLPer100Km.ToString("F1"),
                        t.TotalFuelConsumedL.ToString("F1"),
                        t.TruckDamagePercent.ToString("F1"),
                        t.TrailerDamagePercent.ToString("F1"),
                        t.CargoDamagePercent.ToString("F1"),
                        t.Income,
                        CsvEscape($"{t.TruckBrand} {t.TruckName}".Trim()),
                        CsvEscape(t.GameType)));
                }

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TripLogbook] Failed to export CSV: {ex.Message}");
            }
        }

        public void ExportToJson(List<TripRecord> trips, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(trips, WriteOptions);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TripLogbook] Failed to export JSON: {ex.Message}");
            }
        }

        private static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
