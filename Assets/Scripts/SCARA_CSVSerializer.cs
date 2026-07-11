using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

public static class SCARA_CSVSerializer
{
    private static int saveCounter = 0;

    public static string Save(TrajectorySample[] data, string folder)
    {
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string unique = (saveCounter++).ToString("D3") + "_" + DateTime.UtcNow.Ticks.ToString();
        string filename = string.Format("SCARA_Trajectory_{0}_{1}.csv", timestamp, unique);
        string path = Path.Combine(folder, filename);

        using (StreamWriter w = new StreamWriter(path, false, Encoding.UTF8))
        {
            w.WriteLine("Time,X,Z,Theta1,Theta2");
            for (int i = 0; i < data.Length; i++)
            {
                var s = data[i];
                w.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F3},{1:F6},{2:F6},{3:F6},{4:F6}", s.time, s.x, s.z, s.theta1, s.theta2));
            }
        }
        return path;
    }

    public static bool TryLoad(string path, out TrajectorySample[] data, out string error)
    {
        data = null;
        error = null;

        if (!File.Exists(path)) { error = "File not found: " + path; return false; }

        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch (Exception e) { error = "Read failed: " + e.Message; return false; }

        if (lines.Length < 2) { error = "File has no data rows."; return false; }

        List<TrajectorySample> parsed = new List<TrajectorySample>(lines.Length);
        double lastTime = -1;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0) continue;

            string[] parts = line.Split(',');
            if (parts.Length != 5)
            {
                error = string.Format("Row {0}: expected 5 columns, found {1}. Line: '{2}'", i, parts.Length, line);
                return false;
            }

            double timeTemp = 0;
            float xTemp = 0, zTemp = 0, th1Temp = 0, th2Temp = 0;
            bool ok = true;
            string badPart = "";

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out timeTemp))
            {
                ok = false;
                badPart = parts[0];
            }

            if (ok && !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out xTemp))
            {
                ok = false;
                badPart = parts[1];
            }

            if (ok && !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out zTemp))
            {
                ok = false;
                badPart = parts[2];
            }

            if (ok && !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out th1Temp))
            {
                ok = false;
                badPart = parts[3];
            }

            if (ok && !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out th2Temp))
            {
                ok = false;
                badPart = parts[4];
            }

            if (!ok)
            {
                error = string.Format("Row {0}: malformed number at '{1}'. Expected numeric format (e.g. 1.234). Line: '{2}'",
                                      i, badPart, line);
                return false;
            }

            double t = timeTemp;
            float x = xTemp, z = zTemp, th1 = th1Temp, th2 = th2Temp;

            if (double.IsNaN(t) || double.IsInfinity(t) || float.IsNaN(x) || float.IsInfinity(x) ||
                float.IsNaN(z) || float.IsInfinity(z) || float.IsNaN(th1) || float.IsInfinity(th1) ||
                float.IsNaN(th2) || float.IsInfinity(th2))
            {
                error = string.Format("Row {0}: NaN or Infinity value detected. Line: '{1}'", i, line);
                return false;
            }

            if (t < 0)
            {
                error = string.Format("Row {0}: negative timestamp ({1}). Line: '{2}'", i, t, line);
                return false;
            }

            if (t <= lastTime)
            {
                error = string.Format("Row {0}: duplicate or non-increasing timestamp ({1} <= {2}). Line: '{3}'",
                                      i, t, lastTime, line);
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
            error = string.Format("Fewer than 2 valid rows after parsing (found {0}).", parsed.Count);
            return false;
        }

        data = parsed.ToArray();
        return true;
    }
}