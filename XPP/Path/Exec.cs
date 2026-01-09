
using System;

namespace XPP.Path;

public ref partial struct Exec<Nav>(XPath Path) where Nav : IXPathNav<Nav>
{
  public const int MAX_SORT_NODESET = 0x10000;
  public const int MAX_DATA_SIZE = 0x10000;
  private struct PathState
  {
    public Nav Nav;
    public int Index;
    public Range Nodes;

    public Nav Base;

    public bool Up;
    public int Depth;

    public bool FirstEnd;
    public bool SecondEnd;
  }

  private class ResultBuf : ThreadBuf<ResultBuf, Nav>, IThreadBuf
  {
    public static int Size => MAX_SORT_NODESET;
  };
  private class DedupeBuf : ThreadBuf<DedupeBuf, Nav>, IThreadBuf
  {
    public static int Size => MAX_SORT_NODESET;
  };
  private class DataBuf : ThreadBuf<DataBuf, char>, IThreadBuf
  {
    public static int Size => MAX_DATA_SIZE;
  }
  private class StringBuf : ThreadBuf<StringBuf, Range>, IThreadBuf
  {
    public static int Size => MAX_SORT_NODESET;
  }
  private class NumberBuf : ThreadBuf<NumberBuf, double>, IThreadBuf
  {
    public static int Size => MAX_SORT_NODESET;
  }

  private const string DEFAULT_DATA = "truefalseNaN0-Infinity";
  private static readonly Range TRUE_DATA = ^0..^4;
  private static readonly Range FALSE_DATA = ^4..^9;
  private static readonly Range NAN_DATA = ^9..^12;
  private static readonly Range ZERO_DATA = ^12..^13;
  private static readonly Range PINF_DATA = ^14..^22;
  private static readonly Range NINF_DATA = ^13..^22;

  public readonly XPath Path = Path;

  private readonly PathState[] states = new PathState[Path.Paths.Length];
  private SpanBuf<Nav> resultBuf = ResultBuf.Buf;
  private readonly Span<Nav> dedupeBuf = DedupeBuf.Span;
  private SpanBuf<char> data = DataBuf.Buf;
  private SpanBuf<Range> stringBuf = StringBuf.Buf;
  private SpanBuf<double> numberBuf = NumberBuf.Buf;

  public ReadOnlySpan<char> String(Range range)
  {
    if (range.Start.IsFromEnd)
      return DEFAULT_DATA.AsSpan()[range.Start.Value..range.End.Value];
    return data[range];
  }

  public TypedValue Run(Nav ctx)
  {
    if (data.Length == 0 && Path.Data.Length > 0)
      data.AddRange(Path.Data);
    return GetValue(0, ctx);
  }

  public bool NextNode(int idx, out Nav nav)
  {
    ref readonly var op = ref Path.Paths[idx];
    ref var state = ref states[idx];
    switch (op.Type)
    {
      case PathOpType.Context:
        if (state.Index++ > 0)
        {
          nav = default;
          return false;
        }
        nav = state.Nav.Clone();
        return true;
      case PathOpType.Root:
        if (state.Index++ > 0)
        {
          nav = default;
          return false;
        }
        nav = state.Nav.Root().Clone();
        return true;
      case PathOpType.Union:
        return NextUnion(idx, out nav);
      case PathOpType.Axis:
        return NextAxis(idx, out nav);
      case PathOpType.NodeType:
        while (NextNode(idx + 1, out state.Nav))
        {
          if (op.NodeType == NodeType.Node || state.Nav.Type() == op.NodeType)
          {
            state.Index++;
            nav = state.Nav.Clone();
            return true;
          }
        }
        nav = default;
        return false;
      case PathOpType.NameTest:
        while (NextNode(idx + 1, out state.Nav))
        {
          if (NameTest(in op, ref state.Nav))
          {
            state.Index++;
            nav = state.Nav.Clone();
            return true;
          }
        }
        nav = default;
        return false;
      case PathOpType.Filter:
        while (NextNode(idx + 1, out state.Nav))
        {
          var val = GetValue(op.Filter, state.Nav);
          if (val.Type switch
          {
            ValueType.Number => throw new NotImplementedException(),
            _ => BoolValue(val),
          })
          {
            nav = state.Nav.Clone();
            return true;
          }
        }
        nav = default;
        return false;
      case PathOpType.Normalize:
        if (state.Index == -1)
        {
          if (op.Dedupe)
            NormalizeDedupe(idx, ref state);
          else
            Normalize(idx, ref state);
        }
        var norm = resultBuf[state.Nodes];
        if (++state.Index < norm.Length)
        {
          nav = norm[state.Index];
          return true;
        }
        nav = default;
        return false;
      default:
        throw new InvalidOperationException($"{op.Type}");
    }
  }

  private bool NameTest(ref readonly PathOp op, ref Nav nav)
  {
    var ns = Path.Data.AsSpan(op.Ns);
    var name = Path.Data.AsSpan(op.Name);
    return (ns.Length, name.Length) switch
    {
      (_, > 0) => nav.HasNs(ns) && nav.HasName(name),
      ( > 0, 0) => nav.HasName(ns),
      _ => true,
    };
  }

  private void Normalize(int idx, ref PathState state)
  {
    var start = resultBuf.Length;
    while (NextNode(idx + 1, out var nav))
      resultBuf.Add(nav.Clone());

    state.Nodes = start..resultBuf.Length;
    resultBuf[state.Nodes].Sort();
  }

  private void NormalizeDedupe(int idx, ref PathState state)
  {
    var buf = new SpanBuf<Nav>(dedupeBuf);

    while (NextNode(idx + 1, out var nav))
    {
      if (buf.Length == buf.Cap)
        Compact(ref buf);
      buf.Add(nav.Clone());
    }
    Compact(ref buf);
    state.Nodes = resultBuf.AddRange(buf.Span);
  }

  private static void Compact(ref SpanBuf<Nav> buf)
  {
    if (buf.Length < 2)
      return;
    buf.Span.Sort();
    var count = 1;
    for (var i = 1; i < buf.Length; i++)
    {
      if (buf[count - 1].CompareTo(buf[i]) != 0)
        buf[count++] = buf[i];
    }
    buf[count..buf.Length].Clear();
    buf.Length = count;
  }

  private bool NextUnion(int idx, out Nav nav)
  {
    ref var state = ref states[idx];
    ref readonly var op = ref Path.Paths[idx];
    var (p1, p2) = op.Paths;
    if (state.Index++ == 0)
    {
      ResetPath(p1, state.Nav);
      ResetPath(p2, state.Nav);
      state.FirstEnd = !NextNode(p1, out state.Base);
      state.SecondEnd = !NextNode(p2, out state.Nav);
    }

    var next1 = false;
    var next2 = false;
    var res = false;

    if (state.FirstEnd && state.SecondEnd)
      nav = default;
    else if (state.FirstEnd)
    {
      nav = state.Nav;
      next2 = true;
      res = true;
    }
    else if (state.SecondEnd)
    {
      nav = state.Base;
      next1 = true;
      res = true;
    }
    else
    {
      var cmp = state.Base.CompareTo(state.Nav);
      if (cmp < 0)
      {
        nav = state.Base;
        next1 = true;
      }
      else if (cmp > 0)
      {
        nav = state.Nav;
        next2 = true;
      }
      else
      {
        nav = state.Base;
        next1 = true;
        next2 = true;
      }
      res = true;
    }

    if (next1)
    {
      state.FirstEnd = !NextNode(p1, out var first);
      state.Base = first;
    }
    if (next2)
    {
      state.SecondEnd = !NextNode(p2, out var second);
      state.Nav = second;
    }
    return res;
  }

  private void ResetPath(int idx, Nav ctx)
  {
    while (!Path.Paths[idx].Type.IsRoot)
    {
      ref var state = ref states[idx++];
      state.Index = -1;
      var (start, end) = (state.Nodes.Start.Value, state.Nodes.End.Value);
      if (end > start && start > resultBuf.Length)
        resultBuf.Length = start;
      state.Nodes = 0..0;
    }
    states[idx] = new() { Nav = ctx.Clone(), Index = 0 };
  }
}

public interface IXPathNav<Nav> : IComparable<Nav> where Nav : IXPathNav<Nav>
{
  public Nav Clone();
  public Nav Root();
  public NodeType Type();
  public bool IsAttribute();
  public bool IsNs();

  public bool HasNs(ReadOnlySpan<char> ns);
  public bool HasName(ReadOnlySpan<char> name);

  public bool Parent(out Nav nav);
  public bool FirstChild(out Nav nav);
  public bool LastChild(out Nav nav);
  public bool NextSibling(out Nav nav);
  public bool PreviousSibling(out Nav nav);
  public bool FirstAttribute(out Nav nav);
  public bool NextAttribute(out Nav nav);
  public bool FirstNamespace(out Nav nav);
  public bool NextNamespace(out Nav nav);

  public int StringValue(Span<char> buffer);
}