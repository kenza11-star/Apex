using System;
using System.IO;
using System.Numerics;

namespace ApexUrpLab
{
    public sealed class LapCourse
    {
        public readonly Vector3[] Points;
        public readonly Rail[] Rails;
        private readonly float[] offsets;
        public float Length => offsets[offsets.Length - 1];

        public readonly struct Rail
        {
            public readonly Vector3 A, B;
            public readonly float Height;
            public Rail(Vector3 a, Vector3 b, float height) { A = a; B = b; Height = height; }
        }

        public readonly struct Frame
        {
            public readonly Vector3 Position, Forward;
            public Frame(Vector3 position, Vector3 forward)
            {
                Position = position;
                Forward = forward;
            }
            public Vector3 Right => new Vector3(Forward.Z, 0, -Forward.X);
        }

        public LapCourse(Vector3[] points, Rail[] rails)
        {
            Points = points;
            Rails = rails;
            if (points.Length < 4) throw new InvalidDataException("Course must be closed with distinct points.");
            offsets = new float[Points.Length + 1];
            for (int i = 0; i < Points.Length; i++)
            {
                float segment = Vector3.Distance(Points[i], Points[(i + 1) % Points.Length]);
                if (segment < .001f || float.IsNaN(segment) || float.IsInfinity(segment))
                    throw new InvalidDataException("Invalid course segment " + i);
                offsets[i + 1] = offsets[i] + segment;
            }
        }

        public static LapCourse Decode(byte[] blob)
        {
            using var reader = new BinaryReader(new MemoryStream(blob, false));
            if (reader.ReadUInt32() != 0x31585041)
                throw new InvalidDataException("Unknown APEX track data.");
            int pointCount = reader.ReadInt32(), railCount = reader.ReadInt32();
            if (pointCount < 4 || pointCount > 100_000 || railCount < 0 || railCount > 100_000 ||
                reader.BaseStream.Length != 12L + 12L * pointCount + 28L * railCount)
                throw new InvalidDataException("Invalid APEX track data lengths.");
            var points = new Vector3[pointCount];
            var rails = new Rail[railCount];
            Vector3 ReadPoint() => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            for (int i = 0; i < pointCount; i++) points[i] = ReadPoint();
            for (int i = 0; i < railCount; i++) rails[i] = new Rail(ReadPoint(), ReadPoint(), reader.ReadSingle());
            return new LapCourse(points, rails);
        }

        public Frame At(float distance, float lane = 0)
        {
            float arc = (distance % Length + Length) % Length;
            int index = Array.BinarySearch(offsets, arc);
            index = Math.Min(index >= 0 ? index : ~index - 1, Points.Length - 1);
            int next = (index + 1) % Points.Length;
            float t = (arc - offsets[index]) / (offsets[index + 1] - offsets[index]);
            Vector3 tangent = Points[next] - Points[(index + Points.Length - 1) % Points.Length];
            Vector3 future = Points[(next + 1) % Points.Length] - Points[index];
            Vector3 forward = Vector3.Lerp(tangent, future, t);
            forward.Y = 0;
            forward = Vector3.Normalize(forward);
            return new Frame(Vector3.Lerp(Points[index], Points[next], t) +
                new Vector3(forward.Z, 0, -forward.X) * lane, forward);
        }

        public float NearestArc(Vector3 position, out float lateralDistance)
        {
            float best = float.PositiveInfinity, arc = 0;
            position.Y = 0;
            for (int i = 0; i < Points.Length; i++)
            {
                Vector3 a = Points[i], b = Points[(i + 1) % Points.Length];
                a.Y = b.Y = 0;
                Vector3 direction = b - a;
                float directionLength = direction.LengthSquared();
                if (directionLength < .00001f) continue;
                float t = Math.Clamp(Vector3.Dot(position - a, direction) / directionLength, 0, 1);
                float error = Vector3.DistanceSquared(position, a + direction * t);
                if (error >= best) continue;
                best = error;
                arc = offsets[i] + (offsets[i + 1] - offsets[i]) * t;
            }
            lateralDistance = MathF.Sqrt(best);
            return arc % Length;
        }
    }

    public sealed class LapTimer
    {
        public const int GateCount = 28;
        public int NextGate { get; private set; }
        public int LapCount { get; private set; }
        public float Elapsed { get; private set; }
        public bool Active { get; private set; }
        private readonly LapCourse.Frame[] gates = new LapCourse.Frame[GateCount];

        public LapTimer(LapCourse course)
        {
            for (int i = 0; i < gates.Length; i++) gates[i] = course.At(course.Length * i / GateCount);
        }

        public void Invalidate()
        {
            Active = false;
            NextGate = 0;
            Elapsed = 0;
        }

        // Reports a lap only after crossing all gates in order, in the correct direction.
        public float Observe(Vector3 previous, Vector3 current, float dt)
        {
            if (dt <= 0) return 0;
            if (Active) Elapsed += dt;
            var gate = gates[NextGate];
            float start = Vector3.Dot(previous - gate.Position, gate.Forward);
            float end = Vector3.Dot(current - gate.Position, gate.Forward);
            if (start >= 0 || end < 0 || end <= start) return 0;
            Vector3 crossing = Vector3.Lerp(previous, current, -start / (end - start)) - gate.Position;
            if (MathF.Abs(Vector3.Dot(crossing, gate.Right)) > 7.05f || MathF.Abs(crossing.Y) > 2.5f)
                return 0;
            float completed = 0;
            if (NextGate == 0)
            {
                if (Active)
                {
                    completed = Elapsed;
                    LapCount++;
                }
                Active = true;
                Elapsed = 0;
            }
            NextGate = (NextGate + 1) % gates.Length;
            return completed;
        }
    }
}
