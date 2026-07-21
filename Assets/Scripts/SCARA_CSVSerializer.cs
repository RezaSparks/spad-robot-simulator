using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

public static class SCARA_CSVSerializer
{
    /// <summary>
    /// Saves trajectory data to a CSV file with a metadata header and optional waypoint section.
    /// </summary>
    public static bool Save(TrajectorySample[] data, string filePath, float arm1, float arm2, float maxSpeed, out string error)
    {
        return Save(data, filePath, arm1, arm2, maxSpeed, null, out error);
    }

    /// <summary>
    /// Saves trajectory data + optional waypoint list. Waypoints are written before the data.
    /// </summary>
    public static bool Save(TrajectorySample[] data, string filePath, float arm1, float arm2, float maxSpeed,
                            List<Waypoint> waypoints, out string error)
    {
        error = null;

        if (data == null || data.Length == 0)
        {
            error = "No trajectory data to save.";
            return false;
        }

        try
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (StreamWriter w = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // --- METADATA HEADER ---
                w.WriteLine($"# ROBOT PARAMETERS: Arm1={arm1:F4}, Arm2={arm2:F4}, MaxSpeed={maxSpeed:F2}, Samples={data.Length}");
                w.WriteLine($"# Generated: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");

                // --- WAYPOINTS SECTION (if provided) – written BEFORE the data ---
                if (waypoints != null && waypoints.Count > 0)
                {
                    w.WriteLine("# WAYPOINTS:");
                    for (int i = 0; i < waypoints.Count; i++)
                    {
                        var wp = waypoints[i];
                        w.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "# WP: {0:F6},{1:F6},{2:F6},{3:F6},{4:F0}",
                            wp.xz.x, wp.xz.y, wp.theta1Deg, wp.theta2Deg, wp.speedPercent));
                    }
                    w.WriteLine(); // blank line for readability
                }

                // --- DATA HEADER ---
                w.WriteLine("Time,X,Z,Theta1,Theta2");

                // --- DATA ROWS ---
                for (int i = 0; i < data.Length; i++)
                {
                    var s = data[i];
                    w.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0:F3},{1:F6},{2:F6},{3:F6},{4:F6}",
                        s.time, s.x, s.z, s.theta1, s.theta2));
                }
            }
            return true;
        }
        catch (Exception e)
        {
            error = $"Save failed: {e.Message}";
            return false;
        }
    }

    /// <summary>
    /// Legacy auto-save (internal use) – now writes without waypoints.
    /// </summary>
    public static string SaveAuto(TrajectorySample[] data, string folder, float arm1, float arm2, float maxSpeed)
    {
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"Trajectory_{timestamp}.csv";
        string path = Path.Combine(folder, filename);

        string error;
        bool success = Save(data, path, arm1, arm2, maxSpeed, null, out error);
        return success ? path : null;
    }

    /// <summary>
    /// Loads a CSV trajectory and optionally returns embedded waypoints.
    /// Parses waypoints wherever they appear (# WP: lines) and data rows.
    /// </summary>
    public static bool TryLoad(string path, out TrajectorySample[] data, out List<Waypoint> waypoints, out string error)
    {
        data = null;
        waypoints = null;
        error = null;

        if (!File.Exists(path)) { error = "File not found: " + path; return false; }

        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch (Exception e) { error = "Read failed: " + e.Message; return false; }

        if (lines.Length < 2) { error = "File has no data rows."; return false; }

        List<TrajectorySample> parsed = new List<TrajectorySample>(lines.Length);
        List<Waypoint> parsedWaypoints = new List<Waypoint>();
        double lastTime = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0) continue;

            // Parse waypoint lines (they are comments starting with "# WP:")
            if (line.StartsWith("# WP:"))
            {
                string wpLine = line.Substring(5).Trim(); // remove "# WP:"
                string[] wpParts = wpLine.Split(',');
                if (wpParts.Length == 5)
                {
                    float wpX, wpZ, wpTheta1, wpTheta2, wpSpeed;
                    if (float.TryParse(wpParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out wpX) &&
                        float.TryParse(wpParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out wpZ) &&
                        float.TryParse(wpParts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out wpTheta1) &&
                        float.TryParse(wpParts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out wpTheta2) &&
                        float.TryParse(wpParts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out wpSpeed))
                    {
                        parsedWaypoints.Add(new Waypoint
                        {
                            xz = new Vector2(wpX, wpZ),
                            theta1Deg = wpTheta1,
                            theta2Deg = wpTheta2,
                            speedPercent = wpSpeed
                        });
                    }
                }
                continue;
            }

            // Skip other comment lines (e.g. metadata, "WAYPOINTS" label)
            if (line.StartsWith("#")) continue;

            // Skip the header line
            if (line.StartsWith("Time") || line.StartsWith("time")) continue;

            // --- Parse a data row ---
            string[] parts = line.Split(',');
            if (parts.Length != 5)
            {
                error = $"Row {i + 1}: expected 5 columns, found {parts.Length}. Line: '{line}'";
                return false;
            }

            double timeTemp = 0;
            float xTemp = 0, zTemp = 0, th1Temp = 0, th2Temp = 0;
            bool ok = true;
            string badPart = "";

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out timeTemp))
            { ok = false; badPart = parts[0]; }
            if (ok && !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out xTemp))
            { ok = false; badPart = parts[1]; }
            if (ok && !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out zTemp))
            { ok = false; badPart = parts[2]; }
            if (ok && !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out th1Temp))
            { ok = false; badPart = parts[3]; }
            if (ok && !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out th2Temp))
            { ok = false; badPart = parts[4]; }

            if (!ok)
            {
                error = $"Row {i + 1}: malformed number at '{badPart}'. Line: '{line}'";
                return false;
            }

            double t = timeTemp;
            float x = xTemp, z = zTemp, th1 = th1Temp, th2 = th2Temp;

            if (double.IsNaN(t) || double.IsInfinity(t) || float.IsNaN(x) || float.IsInfinity(x) ||
                float.IsNaN(z) || float.IsInfinity(z) || float.IsNaN(th1) || float.IsInfinity(th1) ||
                float.IsNaN(th2) || float.IsInfinity(th2))
            {
                error = $"Row {i + 1}: NaN or Infinity detected. Line: '{line}'";
                return false;
            }

            if (t < 0)
            {
                error = $"Row {i + 1}: negative timestamp ({t}). Line: '{line}'";
                return false;
            }

            if (t <= lastTime && lastTime >= 0)
            {
                error = $"Row {i + 1}: duplicate or non-increasing timestamp ({t} <= {lastTime}). Line: '{line}'";
                return false;
            }

            lastTime = t;
            parsed.Add(new TrajectorySample
            {
                time = (float)t,
                x = x,
                z = z,
                theta1 = th1,
                theta2 = th2
            });
        }

        if (parsed.Count < 2)
        {
            error = $"Fewer than 2 valid rows after parsing (found {parsed.Count}).";
            return false;
        }

        data = parsed.ToArray();
        waypoints = parsedWaypoints.Count > 0 ? parsedWaypoints : null;
        return true;
    }
}