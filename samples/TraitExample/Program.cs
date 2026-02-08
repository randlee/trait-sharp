using System;
using TraitSharp.Runtime;
using TraitExample;

// ─────────────────────────────────────────────────────────────────────────────
// TraitSharp Example
// Demonstrates zero-cost polymorphism over value types using trait emulation.
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine("=== TraitSharp Example ===");
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

// --- Multiple types implementing the same trait ---
Console.WriteLine("--- Generic algorithm: Distance ---");

var point3d = new Point3D { X = 30, Y = 40, Z = 1.5f };

// Distance works across different types that implement ICoordinate
float dist = Algorithms.Distance(ref dp, ref point3d);
Console.WriteLine($"Distance(DataPoint({dp.X},{dp.Y}), Point3D({point3d.X},{point3d.Y})): {dist:F2}");
Console.WriteLine();

// --- Color trait ---
Console.WriteLine("--- Color trait: Luminance ---");
byte luma = Algorithms.ToLuminance(ref dp);
Console.WriteLine($"Luminance of ({dp.R}, {dp.G}, {dp.B}): {luma}");
Console.WriteLine();

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

Console.WriteLine("=== Done ===");
