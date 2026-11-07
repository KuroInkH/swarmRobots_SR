using System.Collections;
using UnityEngine;

// Resonance - Vibration. After the Messenger's introduction the robots form one complete ring
// around the mat centre that travels briskly while every robot shakes in and out of its place,
// slightly out of step with its neighbours. The shaking fades and the turning slows to a calm,
// even pace (same centre, direction and order), the ring turns smoothly with no shaking, and at
// 30 s it flows into the line.
public class ResonanceVibration : SwarmRoutine
{
    public override string Name { get { return "Resonance: vibration"; } }
    public override int MinRobots { get { return 2; } }

    // ---- Timing (seconds after the introduction; the return to the line starts at 30) ----
    const float FormSecs = 3f;     // line -> ring, already turning and shaking
    const float SettleFrom = 5f;   // the shaking starts to fade and the turning to slow...
    const float SettleTo = 20f;    // ...until the ring turns calmly with no shaking

    // ---- Ring ----
    const float Radius = 95f;
    const float BriskSpeed = 70f;      // mat units per second along the ring at the start
    const float CalmSpeed = 16f;       // from 20 s on, easing to a stop during the return
    const float ShakeSize = 10f;       // in and out of its place (mat units)
    const float ShakeHz = 2f;          // shakes per second at the start
    const float ShakeStagger = 0.18f;  // each robot shakes this much of a shake after the one before it

    public override IEnumerator Run(RoutineContext ctx) { return Show.Run(ctx, start => new Plan(start)); }

    public class Plan : Show.IPlan
    {
        readonly int n;
        readonly Vector2[] start, spot;
        readonly float[] place;   // radians around the ring, fixed per robot
        readonly float[] fromAngle, fromRadius, swing;   // where each robot starts, around the centre
        readonly Show.Eased speed = new Show.Eased(0f, BriskSpeed, SettleFrom, BriskSpeed, SettleTo, CalmSpeed, Show.PerformSecs, CalmSpeed, Show.Total, 0f);
        readonly Show.Eased shakeSize = new Show.Eased(0f, ShakeSize, SettleFrom, ShakeSize, SettleTo, 0f);
        readonly Show.Eased shakeHz = new Show.Eased(0f, ShakeHz, SettleFrom, ShakeHz, SettleTo, 0f);

        public Plan(Vector2[] start)
        {
            this.start = start;
            n = start.Length;
            place = Show.RingPlaces(start, Radius, Turned(FormSecs));
            fromAngle = new float[n]; fromRadius = new float[n]; swing = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 d = start[i] - Shapes.Centre;
                fromAngle[i] = Mathf.Atan2(d.y, d.x);
                fromRadius[i] = d.magnitude;
                // A robot (almost) on the centre has no direction of its own: it heads straight out
                if (fromRadius[i] < 10f) { fromAngle[i] = place[i] + Turned(FormSecs); }
                swing[i] = Mathf.DeltaAngle(fromAngle[i] * Mathf.Rad2Deg, (place[i] + Turned(FormSecs)) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            }
            var leaving = new Vector2[n];
            for (int i = 0; i < n; i++) { leaving[i] = Ring(i, Show.PerformSecs + Show.ReturnSecs / 2f); }
            spot = Show.LineSpots(leaving);
        }

        float Turned(float t) { return speed.Integral(t) / Radius; }

        float Shake(int i, float t)
        {
            float order = place[i] / (2f * Mathf.PI) * n;   // position around the ring, 0..n
            return shakeSize.At(t) * Mathf.Sin(2f * Mathf.PI * (shakeHz.Integral(t) - order * ShakeStagger));
        }

        Vector2 Ring(int i, float t) { return Show.OnRing(place[i] + Turned(t), Radius + Shake(i, t)); }

        public Vector2 Position(int i, float t)
        {
            t = Mathf.Clamp(t, 0f, Show.Total);
            // Forming: each robot swings round the centre to its place while moving in or out to the
            // ring, turning with the ring from the start, so robots keep clear of each other
            float e = Shapes.Ease(t / FormSecs);
            float angle = fromAngle[i] + e * (swing[i] + Turned(t) - Turned(FormSecs));
            float radius = Mathf.Lerp(fromRadius[i], Radius + Shake(i, t), e);
            Vector2 p = Shapes.Centre + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return Show.Return(p, spot[i], t);
        }

        public Vector2 LineSpot(int i) { return spot[i]; }
    }
}
