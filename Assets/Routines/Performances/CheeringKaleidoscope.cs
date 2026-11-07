using System.Collections;
using UnityEngine;

// Cheering - Kaleidoscope. After the Messenger's introduction the robots form a centred circle
// that eases into a slow turn. Still turning, the circle grows three corners (triangle), then
// becomes a square, then circle, triangle and square again. Robots keep their order around the
// outline, so every change is a short shift to a nearby place. At 30 s the square opens like a
// book: its left and right halves sweep outward and straighten into the two halves of the line,
// the last of the turning carried into the opening.
public class CheeringKaleidoscope : SwarmRoutine
{
    public override string Name { get { return "Cheering: kaleidoscope"; } }
    public override int MinRobots { get { return 2; } }

    // ---- Timing (seconds after the introduction; the book opens at 30) ----
    const float FormSecs = 4f;        // line -> circle
    const float TurnUpSecs = 4f;      // turning eases in...
    const float TurnDownSecs = 4f;    // ...and out after 30 s, while the book opens
    const float TotalTurnDeg = 360f;  // one full turn, so the square ends square to the line
    // Shapes, in order (0 = circle, else number of corners), and when each change starts and ends
    static readonly int[] Corners = { 0, 3, 4, 0, 3, 4 };
    static readonly float[] ChangeFrom = { 0f, 6f, 13f, 20f, 23f, 26f };
    static readonly float[] ChangeTo = { 0f, 9.5f, 16.5f, 21.5f, 24.5f, 28f };

    // ---- Size ----
    const float Outline = 620f;       // same outline length for every shape, so neighbours stay evenly apart

    public override IEnumerator Run(RoutineContext ctx) { return Show.Run(ctx, start => new Plan(start)); }

    public class Plan : Show.IPlan
    {
        readonly int n;
        readonly Vector2[] start, spot;
        readonly float[] along;        // each robot's place along the outline, 0..1 clockwise from the top
        readonly Show.Eased turnRate;  // degrees per second

        public Plan(Vector2[] start)
        {
            this.start = start;
            n = start.Length;
            float rate = TotalTurnDeg / (TurnUpSecs / 2f + (Show.PerformSecs - TurnUpSecs) + TurnDownSecs / 2f);
            turnRate = new Show.Eased(0f, 0f, TurnUpSecs, rate, Show.PerformSecs, rate, Show.PerformSecs + TurnDownSecs, 0f);

            // Places on the outline, evenly spaced and symmetric about the book's spine (vertical,
            // through the centre); robots take them in the order that keeps travel from the line least
            var places = new Vector2[n];
            for (int k = 0; k < n; k++) { places[k] = Shapes.Centre + Rotate(Shape(0, (k + 0.5f) / n), Turned(FormSecs)); }
            var slot = Swarm.LeastTravel(start, places);
            along = new float[n];
            for (int i = 0; i < n; i++) { along[i] = (slot[i] + 0.5f) / n; }

            // Line spots: each half of the (upright) square takes the spots on its own side
            var square = new Vector2[n];
            for (int i = 0; i < n; i++) { square[i] = Shapes.Centre + Shape(4, along[i]); }
            spot = Show.LineSpots(square);
        }

        float Turned(float t) { return turnRate.Integral(t); }

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }

        // Point `share` (0..1) of the way round a shape's outline, clockwise from the top middle,
        // relative to the centre. Polygons stand on a flat top edge.
        static Vector2 Shape(int corners, float share)
        {
            if (corners == 0)
            {
                float r = Outline / (2f * Mathf.PI), a = 2f * Mathf.PI * share - Mathf.PI / 2f;
                return r * new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            float side = Outline / corners, radius = side / (2f * Mathf.Sin(Mathf.PI / corners));
            float d = Mathf.Repeat(share * Outline + side / 2f, Outline);   // from the top-left corner
            int k = Mathf.Min((int)(d / side), corners - 1);
            return Vector2.Lerp(Corner(k, corners, radius), Corner(k + 1, corners, radius), d / side - k);
        }

        static Vector2 Corner(int k, int corners, float radius)
        {
            float a = -Mathf.PI / 2f - Mathf.PI / corners + 2f * Mathf.PI * k / corners;
            return radius * new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        // The shape at time t, part way through a change if one is under way
        Vector2 ShapeAt(int i, float t)
        {
            int k = 0;
            while (k + 1 < Corners.Length && t >= ChangeFrom[k + 1]) { k++; }
            if (k == 0) { return Shape(Corners[0], along[i]); }
            float e = Shapes.Ease((t - ChangeFrom[k]) / (ChangeTo[k] - ChangeFrom[k]));
            return Vector2.Lerp(Shape(Corners[k - 1], along[i]), Shape(Corners[k], along[i]), e);
        }

        public Vector2 Position(int i, float t)
        {
            t = Mathf.Clamp(t, 0f, Show.Total);
            if (t <= Show.PerformSecs)
            {
                Vector2 shape = Shapes.Centre + Rotate(ShapeAt(i, t), Turned(t));
                return Vector2.Lerp(start[i], shape, Shapes.Ease(t / FormSecs));
            }
            // The book opens: each robot swings round the centre onto its own side of the line
            // (left half to the left, right half to the right) while moving to its distance there,
            // so each half sweeps outward and straightens. The turning carries on and dies away.
            float e = Shapes.Ease((t - Show.PerformSecs) / Show.ReturnSecs);
            Vector2 local = Shape(4, along[i]);
            float fromDeg = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            float offset = spot[i].x - Swarm.Centre;
            float toDeg = Mathf.Abs(offset) < 1f ? fromDeg : (offset > 0f ? 0f : 180f);
            float deg = fromDeg + Turned(t) + Mathf.DeltaAngle(fromDeg, toDeg) * e;
            float r = Mathf.Lerp(local.magnitude, Mathf.Abs(offset), e);
            return Shapes.Centre + r * new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
        }

        public Vector2 LineSpot(int i) { return spot[i]; }
    }
}
