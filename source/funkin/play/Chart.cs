using Godot;
using System;
using System.Collections.Generic;

namespace FSlice.Gameplay
{
    /// <summary>
    /// Parsed song chart data.
    /// </summary>
    public class Chart
    {
        public string  SongName = "Unknown";
        public float   Bpm      = 100f;
        public float   Speed    = 2.5f;     // scroll speed (higher = faster)
        public List<NoteData> Notes = new();

        // ──────────────────────────────────────────────────────────────────
        //  JSON loading
        //  Expected format:
        //  {
        //    "song": {
        //      "name":  "Bopeebo",
        //      "bpm":   180,
        //      "speed": 2.5,
        //      "notes": [
        //        { "t": 0.333, "d": 0, "l": 0,   "p": true  },
        //        { "t": 0.666, "d": 2, "l": 0.5, "p": false }
        //      ]
        //    }
        //  }
        //  t = time (seconds), d = direction 0-3, l = hold length, p = mustHit
        // ──────────────────────────────────────────────────────────────────
        public static Chart FromJson(string jsonPath)
        {
            var chart = new Chart();

            if (!FileAccess.FileExists(jsonPath))
            {
                GD.PrintErr($"[Chart] File not found: {jsonPath}");
                return chart;
            }

            using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
            var json = new Json();
            var err  = json.Parse(file.GetAsText());

            if (err != Error.Ok)
            {
                GD.PrintErr($"[Chart] JSON parse error at line {json.GetErrorLine()}: {json.GetErrorMessage()}");
                return chart;
            }

            var root = json.Data.AsGodotDictionary();
            if (!root.ContainsKey("song")) return chart;

            var song = root["song"].AsGodotDictionary();

            if (song.ContainsKey("name"))  chart.SongName = song["name"].AsString();
            if (song.ContainsKey("bpm"))   chart.Bpm      = song["bpm"].AsSingle();
            if (song.ContainsKey("speed")) chart.Speed    = song["speed"].AsSingle();

            if (song.ContainsKey("notes"))
            {
                foreach (var entry in song["notes"].AsGodotArray())
                {
                    var n   = entry.AsGodotDictionary();
                    double t = n.ContainsKey("t") ? n["t"].AsDouble() : 0.0;
                    int    d = n.ContainsKey("d") ? n["d"].AsInt32()  : 0;
                    double l = n.ContainsKey("l") ? n["l"].AsDouble() : 0.0;
                    bool   p = !n.ContainsKey("p") || n["p"].AsBool();
                    chart.Notes.Add(new NoteData(t, d, l, p));
                }

                // Sort ascending by time so the spawner can use a pointer
                chart.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
            }

            GD.Print($"[Chart] Loaded \"{chart.SongName}\" — {chart.Notes.Count} notes @ {chart.Bpm} BPM");
            return chart;
        }

        // ──────────────────────────────────────────────────────────────────
        //  Built-in demo chart (no file needed — useful for quick testing)
        // ──────────────────────────────────────────────────────────────────
        public static Chart Demo()
        {
            const float bpm   = 150f;
            float beat        = 60f / bpm;

            var chart = new Chart { SongName = "Demo", Bpm = bpm, Speed = 2.5f };

            // Simple 4-bar pattern for the player (mustHit = true)
            for (int bar = 0; bar < 4; bar++)
            {
                for (int b = 0; b < 4; b++)
                {
                    double t = (bar * 4 + b) * beat;
                    int    d = (bar + b) % 4;

                    // Make beat 3 of bar 1 a hold note
                    double hold = (bar == 1 && b == 3) ? beat * 2 : 0.0;
                    chart.Notes.Add(new NoteData(t, d, hold, mustHit: true));
                }
            }

            // Simple opponent pattern (mustHit = false, auto-played)
            for (int bar = 0; bar < 4; bar++)
            {
                for (int b = 0; b < 4; b++)
                {
                    double t = (bar * 4 + b) * beat + beat * 0.5;
                    int    d = (b + 2) % 4;
                    chart.Notes.Add(new NoteData(t, d, 0.0, mustHit: false));
                }
            }

            chart.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
            return chart;
        }
    }
}
