using Xunit;

namespace IEC60870.InteropTests;

public sealed class InteropSmokeTests
{
    [Fact(Skip = "Requires live IEC 60870-5-104 peer for verification.")]
    public void RoundTripAgainstReferenceImplementation()
    {
    }
}
