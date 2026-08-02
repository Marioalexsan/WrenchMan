namespace BepinexLogAnalysis;

public class Memorywatch
{
    public long AllocatedBytes { get; private set; }
    private long _referencePoint;
    
    public void ReferencePoint()
    {
        _referencePoint = GC.GetAllocatedBytesForCurrentThread();
    }

    public void Measure()
    {
        var referencePoint = GC.GetAllocatedBytesForCurrentThread();
        AllocatedBytes += referencePoint - _referencePoint;
        _referencePoint = referencePoint;
    }

    public void Reset()
    {
        AllocatedBytes = 0;
    }

    public override string ToString()
    {
        return $"{AllocatedBytes}b | {AllocatedBytes / 1024}kib | {AllocatedBytes / 1048576}mib";
    }
}