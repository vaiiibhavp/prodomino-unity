using Unity.Collections;
using System.Collections.Generic;

public static class FixedListExtensions
{
    // Convert List<int> to FixedList32Bytes<int>
    public static FixedList32Bytes<int> ToFixedList32(this List<int> source)
    {
        FixedList32Bytes<int> fixedList = new FixedList32Bytes<int>();

        // Ensure we do not exceed max capacity (8 for int)
        int count = source.Count > fixedList.Capacity ? fixedList.Capacity : source.Count;

        for (int i = 0; i < count; i++)
        {
            fixedList.Add(source[i]);
        }

        return fixedList;
    }

    public static List<int> FixedListToList(this FixedList32Bytes<int> fixedList)
    {
        var result = new List<int>(fixedList.Length);
        for (int i = 0; i < fixedList.Length; i++)
        {
            result.Add(fixedList[i]);
        }
        return result;
    }
}
