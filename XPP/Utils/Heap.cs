
using System;

namespace XPP.Utils;

public partial class Extensions
{
  public static void Heapify<T>(this AppendList<T> list, bool max = false)
    where T : notnull, IComparable<T>
  {
    for (var index = (list.Length >> 1) - 1; index >= 0; index--)
      BubbleDown(list, index, max);
  }

  public static void BubbleDown<T>(
    this AppendList<T> list, int index = 0, bool max = false
  ) where T : notnull, IComparable<T>
  {
    do
    {
      var l = (index << 1) + 1;
      var r = (index + 1) << 1;
      if (l >= list.Length)
        return;
      if (r < list.Length)
      {
        var lrcmp = list[l].CompareTo(list[r]);
        if ((max && lrcmp < 0) || (!max && lrcmp > 0))
          l = r;
      }
      ref var e = ref list[index];
      ref var c = ref list[l];
      var cmp = e.CompareTo(c);
      if ((max && cmp > 0) || (!max && cmp < 0))
        return;
      (e, c) = (c, e);
      index = l;
    } while (true);
  }

  public static void HeapPop<T>(
    this AppendList<T> list, bool max = false
  ) where T : notnull, IComparable<T>
  {
    if (list.Length == 0)
      throw new InvalidOperationException();
    list[0] = list[^1];
    list.Length--;
    list.BubbleDown(0, max);
  }
}
