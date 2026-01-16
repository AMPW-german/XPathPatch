
using System;

namespace XPP.Utils;

public partial class Extensions
{
  public static void BubbleUp<T>(this AppendList<T> list, int index = 0, bool max = false) where T : notnull, IComparable<T>
  {
    do
    {
      var l = (index << 1) + 1;
      var r = (index + 1) << 1;
      if (l >= list.Length)
        return;
      if (r < list.Length)
      {
        var rcmp = list[r].CompareTo(list[l]);
        if ((max && rcmp > 0) || (!max && rcmp < 0))
          l = r;
      }
      ref var e = ref list[index];
      ref var c = ref list[l];
      var cmp = c.CompareTo(e);
      if ((max && cmp < 0) || (!max && cmp > 0))
        return;
      (e, c) = (c, e);
    } while (true);
  }
}
