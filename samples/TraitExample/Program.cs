using System;
using TraitSharp.Runtime;
using TraitExample;

// ─────────────────────────────────────────────────────────────────────────────
// TraitSharp Integration Tests
// Demonstrates zero-cost polymorphism over value types using trait emulation.
// Assertions validate generated code correctness; non-zero exit on failure.
// ─────────────────────────────────────────────────────────────────────────────

int passCount = 0;
int failCount = 0;

void Assert(bool condition, string message)
{
    if (condition) { passCount++; }
    else { failCount++; Console.WriteLine($"  [FAIL] {message}"); }
}

Console.WriteLine("=== TraitSharp Integration Tests ===");
Console.WriteLine();

// --- Trait definitions and struct implementations ---
Console.WriteLine("--- Direct field access via layout cast ---");

var dp = new DataPoint { X = 10, Y = 20, R = 255, G = 128, B = 64, A = 255 };

// AsCoordinate() returns a zero-copy reference to the coordinate fields
ref readonly var coord = ref dp.AsCoordinate();
Console.WriteLine($"DataPoint as coordinate: ({coord.X}, {coord.Y})");

// AsColorValue() returns a zero-copy reference to the color fields
ref readonly var color = ref dp.AsColorValue();
Console.WriteLine($"DataPoint as color: R={color.R}, G={color.G}, B={color.B}");

// Extension method accessors (also zero-cost, inlined by JIT)
Console.WriteLine($"Via extension methods: X={dp.GetX()}, Y={dp.GetY()}");
Console.WriteLine();

// Assertions for coordinate and color values
Assert(coord.X == 10, "coord.X should be 10");
Assert(coord.Y == 20, "coord.Y should be 20");
Assert(color.R == 255, "color.R should be 255");
Assert(color.G == 128, "color.G should be 128");
Assert(color.B == 64, "color.B should be 64");
Assert(dp.GetX() == 10, "dp.GetX() should be 10");
Assert(dp.GetY() == 20, "dp.GetY() should be 20");

// --- Multiple types implementing the same trait ---
Console.WriteLine("--- Generic algorithm: Distance ---");

var point3d = new Point3D { X = 30, Y = 40, Z = 1.5f };

// Distance works across different types that implement ICoordinate
float dist = Algorithms.Distance(ref dp, ref point3d);
Console.WriteLine($"Distance(DataPoint({dp.X},{dp.Y}), Point3D({point3d.X},{point3d.Y})): {dist:F2}");
Console.WriteLine();

// Assertion for distance calculation
Assert(Math.Abs(dist - 28.28f) < 0.1f, "Distance should be approximately 28.28");

// --- Color trait ---
Console.WriteLine("--- Color trait: Luminance ---");
byte luma = Algorithms.ToLuminance(ref dp);
Console.WriteLine($"Luminance of ({dp.R}, {dp.G}, {dp.B}): {luma}");
Console.WriteLine();

// Assertion for luminance
Assert(luma == 158, "Luminance should be 158");

// --- TraitSpan iteration ---
Console.WriteLine("--- TraitSpan: iterate coordinate views over an array ---");

var points = new DataPoint[]
{
    new() { X = 0, Y = 0, R = 255, G = 0, B = 0, A = 255 },
    new() { X = 10, Y = 20, R = 0, G = 255, B = 0, A = 255 },
    new() { X = 30, Y = 40, R = 0, G = 0, B = 255, A = 255 },
    new() { X = 50, Y = 60, R = 128, G = 128, B = 128, A = 255 },
};

// ReadOnlyTraitSpan - iterate over coordinate view without copying
var coordSpan = points.AsSpan().AsCoordinateSpan();
Console.WriteLine($"Coordinate span length: {coordSpan.Length}");
foreach (ref readonly var c in coordSpan)
{
    Console.WriteLine($"  ({c.X}, {c.Y})");
}
Console.WriteLine();

// Assertion for span length
Assert(coordSpan.Length == 4, "coordSpan.Length should be 4");

// Mutable TraitSpan - modify coordinates in-place
Console.WriteLine("--- Mutable TraitSpan: translate all points ---");
var mutableSpan = points.AsSpan().AsCoordinateTraitSpan();
foreach (ref var c in mutableSpan)
{
    c.X += 100;
    c.Y += 200;
}

// Verify the original array was modified
Console.WriteLine("After translation:");
foreach (var p in points)
{
    Console.WriteLine($"  ({p.X}, {p.Y})");
}
Console.WriteLine();

// Assertions for translation
Assert(points[0].X == 100, "points[0].X should be 100 after translation");
Assert(points[0].Y == 200, "points[0].Y should be 200 after translation");

// --- Zero allocation verification ---
Console.WriteLine("--- Zero allocation verification ---");
long before = GC.GetAllocatedBytesForCurrentThread();

var testDp = new DataPoint { X = 42, Y = 99 };
for (int i = 0; i < 100_000; i++)
{
    ref readonly var c2 = ref testDp.AsCoordinate();
    _ = c2.X + c2.Y;
}

long after = GC.GetAllocatedBytesForCurrentThread();
Console.WriteLine($"100,000 layout casts allocated: {after - before} bytes (expected: 0)");
Console.WriteLine();

// Assertion for zero allocation
Assert((after - before) == 0, "Zero allocations expected for layout casts");

Console.WriteLine("=== Done ===");
Console.WriteLine();

// Print test summary and exit with appropriate code
Console.WriteLine($"Integration Tests: {passCount} passed, {failCount} failed");
return failCount > 0 ? 1 : 0;
