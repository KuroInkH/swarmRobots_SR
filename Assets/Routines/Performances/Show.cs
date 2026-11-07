using System;
using System.Collections;
using System.Linq;
using UnityEngine;

// Shared frame of the performance routines (Resonance and Cheering): the Messenger's full
// introduction, then a 30-second performance planned as one continuous path per robot, then the
// return to LineUp's line, facing the participant. The performance clock starts only when the
// introduction has finished, from wherever the robots actually are.
public static class Show
{
    public const float PerformSecs = 30f;
    public const float ReturnSecs = 8f;   // performance flows into the line over this long
    public const float Total = PerformSecs + ReturnSecs;

    // A performance: robot i's planned mat position t seconds after the introduction (it starts
    // where the robot is, and ends at rest on LineSpot(i) at Total)
    public interface IPlan
    {
        Vector2 Position(int i, float t);
        Vector2 LineSpot(int i);
    }

    public static IEnumerator Run(RoutineContext ctx, Func<Vector2[], IPlan> makePlan, int maxSpeed = 100)
    {
        yield return Messenger.Introduction(ctx);
        int n = ctx.Count;
        var start = new Vector2[n];
        for (int i = 0; i < n; i++) { start[i] = new Vector2(ctx.Cubes[i].x, ctx.Cubes[i].y); }
        var plan = makePlan(start);
        yield return Swarm.Flow(ctx, plan.Position, Total, maxSpeed);
        var spots = new Vector2Int[n];
        for (int i = 0; i < n; i++) { spots[i] = Vector2Int.RoundToInt(plan.LineSpot(i)); }
        yield return LineUp.Finish(ctx, spots);
    }

    // Robots at `from` the least total travel away from LineUp's spots: which spot each takes
    public static Vector2[] LineSpots(Vector2[] from)
    {
        var line = LineUp.Spots(from.Length).Select(s => (Vector2)s).ToArray();
        var slot = Swarm.LeastTravel(from, line);
        return Enumerable.Range(0, from.Length).Select(i => line[slot[i]]).ToArray();
    }

    // The shared return: the performance keeps going while each robot eases onto its line spot
    public static Vector2 Return(Vector2 performing, Vector2 spot, float t)
    {
        return Vector2.Lerp(performing, spot, Shapes.Ease((t - PerformSecs) / ReturnSecs));
    }

    // Point on a ring around the mat centre (angle in radians, increasing = clockwise on the mat)
    public static Vector2 OnRing(float angle, float radius)
    {
        return Shapes.Centre + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // Evenly spaced ring places (radians, before `turned`) for robots at `from`: the rotation and
    // assignment with the least total travel, so robots take short, non-crossing paths onto it
    public static float[] RingPlaces(Vector2[] from, float radius, float turned)
    {
        int n = from.Length;
        var place = new float[n];
        float bestCost = float.MaxValue;
        for (int k = 0; k < 36; k++)
        {
            float a0 = 2f * Mathf.PI / n * k / 36f;
            var spots = Enumerable.Range(0, n).Select(p => OnRing(a0 + 2f * Mathf.PI * p / n + turned, radius)).ToArray();
            var slot = Swarm.LeastTravel(from, spots);
            float cost = Enumerable.Range(0, n).Sum(i => (from[i] - spots[slot[i]]).sqrMagnitude);
            if (cost < bestCost)
            {
                bestCost = cost;
                for (int i = 0; i < n; i++) { place[i] = a0 + 2f * Mathf.PI * slot[i] / n; }
            }
        }
        return place;
    }

    // A value set at keyframes (time, value, time, value, ...), easing from each keyframe's value
    // to the next one's (repeat a value to hold it). Integral gives the amount covered so far:
    // the distance a speed has carried, the cycles a frequency has run, so changing pace never jumps.
    public class Eased
    {
        readonly float[] time, value;

        public Eased(params float[] keys)
        {
            time = new float[keys.Length / 2]; value = new float[keys.Length / 2];
            for (int k = 0; k < time.Length; k++) { time[k] = keys[2 * k]; value[k] = keys[2 * k + 1]; }
        }

        public float At(float t)
        {
            if (t <= time[0]) { return value[0]; }
            for (int k = 0; k + 1 < time.Length; k++)
            {
                if (t < time[k + 1]) { return Mathf.Lerp(value[k], value[k + 1], Shapes.Ease((t - time[k]) / (time[k + 1] - time[k]))); }
            }
            return value[value.Length - 1];
        }

        public float Integral(float t)
        {
            float sum = value[0] * Mathf.Clamp(t, 0f, time[0]);
            for (int k = 0; k + 1 < time.Length && t > time[k]; k++)
            {
                float d = time[k + 1] - time[k];
                float u = (Mathf.Min(t, time[k + 1]) - time[k]) / d;
                sum += d * (value[k] * u + (value[k + 1] - value[k]) * Shapes.EaseIntegral(u));
            }
            int last = time.Length - 1;
            if (t > time[last]) { sum += value[last] * (t - time[last]); }
            return sum;
        }
    }

    // Running total of a rate that has no simple formula (e.g. turning speed that depends on a
    // changing radius), worked out once in small steps
    public class Integrated
    {
        const float Step = 0.02f;
        readonly float[] total, rate;

        public Integrated(Func<float, float> rateAt, float until)
        {
            int steps = Mathf.CeilToInt(until / Step) + 1;
            total = new float[steps + 1]; rate = new float[steps + 1];
            for (int s = 0; s <= steps; s++) { rate[s] = rateAt(s * Step); }
            for (int s = 1; s <= steps; s++) { total[s] = total[s - 1] + (rate[s - 1] + rate[s]) / 2f * Step; }
        }

        public float At(float t)
        {
            float x = Mathf.Clamp(t / Step, 0f, total.Length - 1.001f);
            int s = (int)x; float u = x - s;
            // Hermite between steps, so the rate stays continuous
            float u2 = u * u, u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * total[s] + (u3 - 2f * u2 + u) * rate[s] * Step
                 + (-2f * u3 + 3f * u2) * total[s + 1] + (u3 - u2) * rate[s + 1] * Step;
        }
    }
}
