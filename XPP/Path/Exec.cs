
using System;
using XPP.Doc;
using XPP.Utils;

namespace XPP.Path;

public ref partial struct Exec(XPath path, XPNavigator ctx)
{
  private const int MAX_SORT_NODESET = 0x10000;
  public const int MAX_DATA_SIZE = 0x10000;
  private struct PathState
  {
    public XPNavigator XPNavigator;
    public int Index;
    public Range Nodes;

    public XPNavigator Base;

    public bool Up;
    public int Depth;

    public bool FirstEnd;
    public bool SecondEnd;

    public int NodeSet;
  }

  private class ResultBuf() : ThreadBuf<ResultBuf, XPNavigator>(MAX_SORT_NODESET);
  private class DedupeBuf() : ThreadBuf<DedupeBuf, XPNavigator>(MAX_SORT_NODESET);
  private class DataBuf() : ThreadBuf<DataBuf, char>(MAX_DATA_SIZE);
  private class StringBuf() : ThreadBuf<StringBuf, string>(MAX_SORT_NODESET);

  private const string TRUE = "true";
  private const string FALSE = "false";
  private const string NAN = "NaN";
  private const string ZERO = "0";
  private const string PINF = "Infinity";
  private const string NINF = "-Infinity";

  private readonly XPath path = path;
  private readonly XPNavigator rootContext = ctx;

  private readonly PathState[] states = new PathState[path.Paths.Length];
  private SpanBuf<XPNavigator> resultBuf = ResultBuf.Buf;
  private readonly Span<XPNavigator> dedupeBuf = DedupeBuf.Span;
  private readonly Span<char> dataBuf = DataBuf.Span;
  private SpanBuf<string> stringBuf = StringBuf.Buf;

  private bool started = false;
  private Value result;
  private XPNodeRef resultNode;

  private ReadOnlySpan<char> CompiledString(Range range) =>
    path.Data.AsSpan(range);

  public bool NextNode(int idx, out XPNavigator nav)
  {
    ref readonly var op = ref path.Paths[idx];
    ref var state = ref states[idx];
    switch (op.Type)
    {
      case PathOpType.Context:
        if (++state.Index > 0)
        {
          nav = default;
          return false;
        }
        nav = state.XPNavigator.Clone();
        return true;
      case PathOpType.Root:
        if (++state.Index > 0)
        {
          nav = default;
          return false;
        }
        nav = state.XPNavigator.Root().Clone();
        return true;
      case PathOpType.Union:
        return NextUnion(idx, out nav);
      case PathOpType.Expr:
        if (++state.Index == 0)
        {
          var val = GetValue(op.Expr, state.XPNavigator);
          if (val.Type != XPValueType.NodeSet)
            throw new InvalidOperationException($"Expression must produce NodeSet");
          state.NodeSet = val.NodeSet;
        }
        return NextNode(state.NodeSet, out nav);
      case PathOpType.Axis:
        return NextAxis(idx, out nav);
      case PathOpType.NodeType:
        while (NextNode(idx + 1, out state.XPNavigator))
        {
          state.Index = states[idx + 1].Index;
          if (op.NodeType == NodeType.Node || state.XPNavigator.Type() == op.NodeType)
          {
            nav = state.XPNavigator.Clone();
            return true;
          }
        }
        nav = default;
        return false;
      case PathOpType.NameTest:
        while (NextNode(idx + 1, out state.XPNavigator))
        {
          state.Index = states[idx + 1].Index;
          if (NameTest(in op, ref state.XPNavigator))
          {
            nav = state.XPNavigator.Clone();
            return true;
          }
        }
        nav = default;
        return false;
      case PathOpType.Filter:
        while (NextNode(idx + 1, out state.XPNavigator))
        {
          state.Index = states[idx + 1].Index;
          var val = GetValue(op.Expr, state.XPNavigator);
          if (val.Type switch
          {
            // Index starts at 0, position starts at 1
            XPValueType.Number => (state.Index + 1) == val.Number,
            _ => BoolValue(val),
          })
          {
            nav = state.XPNavigator.Clone();
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

  private bool NameTest(ref readonly PathOp op, ref XPNavigator nav)
  {
    var ns = path.Data.AsSpan(op.Ns);
    var name = path.Data.AsSpan(op.Name);
    return (ns.Length, name.Length) switch
    {
      (_, > 0) => nav.HasNs(ns) && nav.HasName(name),
      ( > 0, 0) => nav.HasNs(ns),
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
    var buf = new SpanBuf<XPNavigator>(dedupeBuf);

    while (NextNode(idx + 1, out var nav))
    {
      if (buf.Length == buf.Cap)
        Compact(ref buf);
      buf.Add(nav.Clone());
    }
    Compact(ref buf);
    state.Nodes = resultBuf.AddRange(buf.Span);
  }

  private static void Compact(ref SpanBuf<XPNavigator> buf)
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

  private bool NextUnion(int idx, out XPNavigator nav)
  {
    ref var state = ref states[idx];
    ref readonly var op = ref path.Paths[idx];
    var (p1, p2) = op.Paths;
    if (++state.Index == 0)
    {
      ResetPath(p1, state.XPNavigator);
      ResetPath(p2, state.XPNavigator);
      state.FirstEnd = !NextNode(p1, out state.Base);
      state.SecondEnd = !NextNode(p2, out state.XPNavigator);
    }

    var next1 = false;
    var next2 = false;
    var res = false;

    if (state.FirstEnd && state.SecondEnd)
      nav = default;
    else if (state.FirstEnd)
    {
      nav = state.XPNavigator;
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
      var cmp = state.Base.CompareTo(state.XPNavigator);
      if (cmp < 0)
      {
        nav = state.Base;
        next1 = true;
      }
      else if (cmp > 0)
      {
        nav = state.XPNavigator;
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
      state.XPNavigator = second;
    }
    return res;
  }

  private void ResetPath(int idx, XPNavigator ctx)
  {
    while (!path.Paths[idx].Type.IsRoot)
    {
      ref var state = ref states[idx++];
      state.Index = -1;
      var (start, end) = (state.Nodes.Start.Value, state.Nodes.End.Value);
      if (end > start && start > resultBuf.Length)
        resultBuf.Length = start;
      state.Nodes = 0..0;
    }
    states[idx] = new() { XPNavigator = ctx.Clone(), Index = -1 };
  }
}