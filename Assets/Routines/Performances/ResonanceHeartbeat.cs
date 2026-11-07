using System.Collections;
using UnityEngine;

// Resonance - Heartbeat. After the Messenger's introduction the robots organise along a row (the
// line) and beat: two strong, quick excursions away from the row and back, close together, then a
// brief release, the pulse travelling down the row. The double beat rounds into a smooth
// travelling wave that slows and flattens until the row is nearly straight, a faint slow ripple
// remains and fades as the robots settle into the line. Large and fast at the start, almost still
// at the end.
public class ResonanceHeartbeat : SwarmRoutine
{
    public override string Name { get { return "Resonance: heartbeat"; } }
    public override int MinRobots { get { return 2; } }

    // ---- Timing (seconds after the introduction; the return to the line starts at 30) ----
    const float EnterSecs = 3f;     // a robot out of the row (the messenger) slides along...
    const float UpDelay = 1.5f;     // ...and steps up into it this much later, clear of the others
    const float BeatEaseSecs = 1.5f; // robots already in the row reach full beats this fast
    const float SoftenFrom = 5f;    // the beat starts to soften...
    const float RoundTo = 14f;      // ...is a smooth wave by here...
    const float SoftenTo = 20f;     // ...and nearly flat by here

    // ---- Beat and wave ----
    const float BeatHz = 0.55f;        // heartbeats per second at the start
    const float CalmHz = 0.18f;        // wave at 20 s
    const float RippleHz = 0.12f;      // faint ripple at 30 s
    const float BeatHeight = 38f;      // mat units away from the row (strong beat)
    const float CalmHeight = 5f;
    const float RippleHeight = 3f;     // fades to nothing during the return
    const float BeatLength = 0.3f;     // each beat lasts this share of a heartbeat...
    const float SecondBeatAt = 0.36f;  // ...the second starts here...
    const float SecondBeatSize = 0.65f; // ...a little smaller; the rest is the release
    const float BeatLag = 0.06f;       // share of a heartbeat between neighbours: the pulse travels
    const float WaveLag = 0.12f;       // same for the smooth wave
    const int MaxSpeed = 115;          // the strong beats need the robots' full speed

    public override IEnumerator Run(RoutineContext ctx) { return Show.Run(ctx, start => new Plan(start), MaxSpeed); }

    public class Plan : Show.IPlan
    {
        readonly Vector2[] start, spot;
        readonly int[] rank;          // place along the row, left to right
        readonly float[] upDelay;
        readonly Show.Eased height = new Show.Eased(0f, BeatHeight, SoftenFrom, BeatHeight, SoftenTo, CalmHeight, Show.PerformSecs, RippleHeight, Show.PerformSecs + Show.ReturnSecs * 0.6f, 0f);
        readonly Show.Eased hz = new Show.Eased(0f, BeatHz, SoftenFrom, BeatHz, SoftenTo, CalmHz, Show.PerformSecs, RippleHz);
        readonly Show.Eased round = new Show.Eased(0f, 0f, SoftenFrom, 0f, RoundTo, 1f);
        readonly Show.Eased lag = new Show.Eased(0f, BeatLag, SoftenFrom, BeatLag, RoundTo, WaveLag);

        public Plan(Vector2[] start)
        {
            this.start = start;
            int n = start.Length;
            spot = Show.LineSpots(start);   // the row is the line: robots in it keep their spot
            rank = new int[n]; upDelay = new float[n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) { if (spot[j].x < spot[i].x) { rank[i]++; } }
                // Robots that have to move along the row first stay below it until they are clear
                upDelay[i] = UpDelay * Mathf.Clamp01(Mathf.Abs(start[i].x - spot[i].x) / Swarm.MinSpacing);
            }
        }

        // One heartbeat (x from 0 to 1): two quick beats, then the release
        static float Beat(float x)
        {
            if (x < BeatLength) { return Bump(x / BeatLength); }
            if (x >= SecondBeatAt && x < SecondBeatAt + BeatLength) { return SecondBeatSize * Bump((x - SecondBeatAt) / BeatLength); }
            return 0f;
        }

        static float Wave(float x) { return Bump(x); }

        static float Bump(float u) { float s = Mathf.Sin(Mathf.PI * u); return s * s; }

        public Vector2 Position(int i, float t)
        {
            t = Mathf.Clamp(t, 0f, Show.Total);
            float phase = hz.Integral(t) - rank[i] * lag.At(t);
            float x = phase - Mathf.Floor(phase);
            // Away from the participant, so beats never reach toward a robot still entering the row
            float y = spot[i].y - height.At(t) * Mathf.Lerp(Beat(x), Wave(x), round.At(t));
            float along = Shapes.Ease(t / EnterSecs);
            float up = Shapes.Ease((t - upDelay[i]) / (upDelay[i] > 0f ? EnterSecs : BeatEaseSecs));
            var p = new Vector2(Mathf.Lerp(start[i].x, spot[i].x, along), Mathf.Lerp(start[i].y, y, up));
            return Show.Return(p, spot[i], t);
        }

        public Vector2 LineSpot(int i) { return spot[i]; }
    }
}
