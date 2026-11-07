using System;
using System.Text;
using UnityEditor;
using UnityEngine;

// Checks the planned paths of the performance routines without robots, for 2 to 8 robots and
// every choice of messenger: closest distance between robots, top speed and acceleration (jumps
// show up as huge accelerations), distance to the mat edge, and that every robot ends at rest on
// its line spot.
//   Unity -batchmode -quit -projectPath <swarm> -executeMethod PerformanceCheck.Run -logFile <log>
public static class PerformanceCheck
{
    const float Dt = 0.01f;

    public static void Run()
    {
        var makers = new (string, Func<Vector2[], Show.IPlan>)[]
        {
            ("Vibration", s => new ResonanceVibration.Plan(s)),
            ("Heartbeat", s => new ResonanceHeartbeat.Plan(s)),
            ("Kaleidoscope", s => new CheeringKaleidoscope.Plan(s)),
            ("SocialDance", s => new CheeringSocialDance.Plan(s)),
        };
        var report = new StringBuilder("\nPERFCHECK results\n");
        foreach (var (name, make) in makers)
        {
            float minDist = float.MaxValue, maxSpeed = 0f, maxAcc = 0f, edge = float.MaxValue, endErr = 0f;
            string minAt = "", accAt = "", edgeAt = "";
            for (int n = 2; n <= 8; n++)
            {
                for (int m = 0; m < n; m++)
                {
                    var start = new Vector2[n];
                    var line = LineUp.Spots(n);
                    for (int i = 0; i < n; i++) { start[i] = line[i]; }
                    start[m] = Messenger.TalkSpot;
                    var plan = make(start);
                    int steps = Mathf.RoundToInt(Show.Total / Dt);
                    var prev = new Vector2[n]; var prev2 = new Vector2[n];
                    for (int k = 0; k <= steps; k++)
                    {
                        float t = k * Dt;
                        var p = new Vector2[n];
                        for (int i = 0; i < n; i++) { p[i] = plan.Position(i, t); }
                        for (int i = 0; i < n; i++)
                        {
                            float e = Mathf.Min(Mathf.Min(p[i].x - Swarm.MatMin, Swarm.MatMax - p[i].x), Mathf.Min(p[i].y - Swarm.MatMin, Swarm.MatMax - p[i].y));
                            if (e < edge) { edge = e; edgeAt = string.Format("n={0} m={1} t={2:F2}", n, m, t); }
                            for (int j = i + 1; j < n; j++)
                            {
                                float d = Vector2.Distance(p[i], p[j]);
                                if (d < minDist) { minDist = d; minAt = string.Format("n={0} m={1} t={2:F2} robots {3},{4}", n, m, t, i, j); }
                            }
                            if (k >= 1) { maxSpeed = Mathf.Max(maxSpeed, (p[i] - prev[i]).magnitude / Dt); }
                            if (k >= 2)
                            {
                                float a = (p[i] - 2f * prev[i] + prev2[i]).magnitude / (Dt * Dt);
                                if (a > maxAcc) { maxAcc = a; accAt = string.Format("n={0} m={1} t={2:F2}", n, m, t); }
                            }
                            prev2[i] = prev[i]; prev[i] = p[i];
                        }
                    }
                    for (int i = 0; i < n; i++)
                    {
                        endErr = Mathf.Max(endErr, (plan.Position(i, Show.Total) - plan.LineSpot(i)).magnitude);
                        endErr = Mathf.Max(endErr, (plan.Position(i, Show.Total) - plan.Position(i, Show.Total - Dt)).magnitude / Dt);
                        endErr = Mathf.Max(endErr, (plan.Position(i, 0f) - start[i]).magnitude);
                    }
                }
            }
            report.AppendFormat("{0}: closest {1:F1} ({2}) | top speed {3:F0} u/s | top accel {4:F0} u/s2 ({5}) | edge {6:F1} ({7}) | start/end error {8:F2}\n",
                name, minDist, minAt, maxSpeed, maxAcc, accAt, edge, edgeAt, endErr);
        }
        Debug.Log(report.ToString());
    }
}
