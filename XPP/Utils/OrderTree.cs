
using System;

namespace XPP.Utils;

public class OrderTree
{
  public readonly struct Key(int KeyIndex)
  {
    public static readonly Key Invalid = new(-1);

    public bool Valid => KeyIndex >= 0;
    public readonly int KeyIndex = KeyIndex;
  }

  private struct KeyNode(int Parent, ulong Min, ulong Max)
  {
    public ulong Min = Min;
    public ulong Max = Max;
    public int Parent = Parent;
    public int Left = -1;
    public int Right = -1;
    public int Size = 1;
  }

  private readonly AppendList<KeyNode> nodes = [new(-1, 0ul, ulong.MaxValue)];
  private readonly AppendList<int> rbStack = [];

  public Key Root => new(0);

  public (Key, Key) Split(Key parent)
  {
    ref var node = ref nodes[parent.KeyIndex];
    if (node.Left == -1)
    {
      var mid = node.Min + ((node.Max - node.Min) >> 1);
      if (mid == node.Min || mid == node.Max)
        Rebalance();
      mid = node.Min + ((node.Max - node.Min) >> 1);
      if (mid == node.Min || mid == node.Max)
        throw new InvalidOperationException();

      node.Left = nodes.Add(new(parent.KeyIndex, node.Min, mid));
      node.Right = nodes.Add(new(parent.KeyIndex, mid, node.Max));
    }
    return (new(node.Left), new(node.Right));
  }

  private void Rebalance()
  {
    // update counts
    for (var i = nodes.Length; --i >= 0;)
    {
      ref var node = ref nodes[i];
      if (node.Left != -1)
        node.Size = 1 + nodes[node.Left].Size + nodes[node.Right].Size;
      else
        node.Size = 1;
    }

    var totalSize = nodes[0].Size;
    var perNode = ulong.MaxValue / (ulong)totalSize;

    rbStack.Length = 0;
    rbStack.Add(0);

    while (rbStack.Length > 0)
    {
      var idx = rbStack[^1];
      rbStack.Length--;

      ref var node = ref nodes[idx];
      if (node.Left == -1)
        continue;

      if (node.Min + 2 > node.Max)
        throw new InvalidOperationException();

      rbStack.Add(node.Right);
      rbStack.Add(node.Left);

      ref var left = ref nodes[node.Left];
      ref var right = ref nodes[node.Right];

      var mid = node.Min + (ulong)left.Size * perNode;
      left.Min = node.Min;
      left.Max = right.Min = mid;
      right.Max = node.Max;
    }
    ref var root = ref nodes[0];
    ref var rleft = ref nodes[root.Left];
    ref var rright = ref nodes[root.Right];
  }

  public int Compare(Key left, Key right)
  {
    // unequal left values compare directly
    ref var lnode = ref nodes[left.KeyIndex];
    ref var rnode = ref nodes[right.KeyIndex];
    var llcmp = lnode.Min.CompareTo(rnode.Min);
    if (llcmp != 0)
      return llcmp;
    // otherwise higher right values are earlier, as it means it is the parent
    return -lnode.Right.CompareTo(rnode.Right);
  }
}

public readonly struct FixedRange(int Start, int End)
{
  public readonly int Start = Start;
  public readonly int End = End;

  public static implicit operator Range(FixedRange range) => range.Start..range.End;
  public static implicit operator FixedRange(Range range)
  {
    if (range.Start.IsFromEnd) throw new InvalidOperationException();
    if (range.End.IsFromEnd) throw new InvalidOperationException();
    return new(range.Start.Value, range.End.Value);
  }
}