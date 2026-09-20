[System.Serializable]
public class FloatRangeValue<T> : IRange
{
    public float min;
    public float max;
    public T value;

    public float Min => min;
    public float Max => max;

    public bool Overlaps(IRange other)
    {
        return min <= other.Max && max >= other.Min;
    }

    public override string ToString() => $"{min:F2} – {max:F2} : {value}";
}
