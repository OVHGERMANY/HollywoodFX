using HollywoodFX.Decal;

var tests = new (string Name, Action Run)[]
{
    ("confirmed penetration creates distinct paired faces", ConfirmedPenetrationCreatesPair),
    ("stopped impact creates no aperture and preserves impact", StoppedImpactCreatesNoAperture),
    ("unmatched exit creates no aperture", UnmatchedExitCreatesNoAperture),
    ("tracker clear rejects stale pooled shot", ClearRejectsStaleShot),
    ("normal incidence keeps a circular physical aperture", NormalIncidenceIsCircular),
    ("grazing incidence stretch is bounded", GrazingIncidenceIsBounded)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine("PASS " + test.Name);
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine("FAIL " + test.Name + ": " + exception.Message);
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
return failures == 0 ? 0 : 1;

static void ConfirmedPenetrationCreatesPair()
{
    var tracker = new PenetrationApertureTracker();
    var shot = new object();
    var entry = tracker.Record(shot, isForwardHit: true, isConfirmedPenetration: true);
    var exit = tracker.Record(shot, isForwardHit: false, isConfirmedPenetration: false);

    Require(entry.CreateAperture, "entry was not created");
    Require(exit.CreateAperture, "exit was not created");
    Require(entry.Face == PenetrationApertureFace.Entry, "near face was misclassified");
    Require(exit.Face == PenetrationApertureFace.Exit, "far face was misclassified");
    Require(entry.PairIdentity == exit.PairIdentity, "faces did not retain one pair identity");
    Require(entry.Identity != exit.Identity, "entry and exit reused one aperture identity");
    Require(entry.PreserveImpact && exit.PreserveImpact, "stock impact preservation was lost");
}

static void StoppedImpactCreatesNoAperture()
{
    var tracker = new PenetrationApertureTracker();
    var result = tracker.Record(new object(), isForwardHit: true, isConfirmedPenetration: false);
    Require(!result.CreateAperture, "a stopped impact created a see-through opening");
    Require(result.PreserveImpact, "the existing stopped-impact mark must remain untouched");
}

static void UnmatchedExitCreatesNoAperture()
{
    var tracker = new PenetrationApertureTracker();
    var result = tracker.Record(new object(), isForwardHit: false, isConfirmedPenetration: false);
    Require(!result.CreateAperture, "an unpaired far-face event created an opening");
}

static void ClearRejectsStaleShot()
{
    var tracker = new PenetrationApertureTracker();
    var shot = new object();
    tracker.Record(shot, isForwardHit: true, isConfirmedPenetration: true);
    tracker.Clear();
    var result = tracker.Record(shot, isForwardHit: false, isConfirmedPenetration: false);
    Require(!result.CreateAperture, "world cleanup left a stale pending entry");
}

static void NormalIncidenceIsCircular()
{
    PenetrationApertureGeometry.ResolveRadii(7.62f, 1f, out var minor, out var major);
    RequireNear(minor, 0.0043815f, 0.000001f, "physical radius");
    RequireNear(major, minor, 0.000001f, "normal-incidence major radius");
}

static void GrazingIncidenceIsBounded()
{
    PenetrationApertureGeometry.ResolveRadii(7.62f, 0.01f, out var minor, out var major);
    RequireNear(
        major,
        minor * PenetrationApertureGeometry.MaximumIncidenceStretch,
        0.000001f,
        "grazing stretch");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void RequireNear(float actual, float expected, float tolerance, string label)
{
    if (Math.Abs(actual - expected) > tolerance)
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
}
