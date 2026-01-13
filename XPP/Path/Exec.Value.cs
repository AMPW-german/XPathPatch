
using System;
using System.Collections.Generic;
using System.Globalization;

namespace XPP.Path;

public struct TypedValue
{
  public ValueType Type;
  public Value Value;

  public static TypedValue MakeBool(bool val) =>
    new() { Type = ValueType.Bool, Value = new() { Bool = val } };
  public static TypedValue MakeNumber(double val) =>
    new() { Type = ValueType.Number, Value = new() { Number = val } };
  public static TypedValue MakeString(Range val) =>
    new() { Type = ValueType.String, Value = new() { String = val } };
  public static TypedValue MakeNodeSet(int val) =>
    new() { Type = ValueType.NodeSet, Value = new() { NodeSet = val } };
}

public ref partial struct Exec<Nav>
{
  private TypedValue GetValue(int idx, Nav ctx)
  {
    ref readonly var op = ref Path.Vals[idx];
    switch (op.Type)
    {
      case ValOpType.Number: return new() { Type = ValueType.Number, Value = op.Value };
      case ValOpType.String: return new() { Type = ValueType.String, Value = op.Value };
      case ValOpType.Variable:
        throw new NotImplementedException();
      case ValOpType.Path:
        ResetPath(op.Left, ctx);
        return TypedValue.MakeNodeSet(op.Left);
      case ValOpType.Negate:
        return TypedValue.MakeNumber(-NumberValue(GetValue(op.Left, ctx)));
      case ValOpType.And or ValOpType.Or: return BoolOp(idx, ctx);
      case ValOpType.Eq or ValOpType.Neq or ValOpType.Lt or ValOpType.Lte or ValOpType.Gt or ValOpType.Gte:
        return CompareOp(idx, ctx);
      case ValOpType.Add or ValOpType.Sub or ValOpType.Mult or ValOpType.Mod or ValOpType.Div:
        return MathOp(idx, ctx);
      case ValOpType.Func:
        throw new NotImplementedException();
      case ValOpType.UserFunc:
        throw new NotImplementedException();
      default:
        throw new InvalidOperationException($"{op.Type}");
    }
  }

  private TypedValue BoolOp(int idx, Nav ctx)
  {
    ref readonly var op = ref Path.Vals[idx];
    var left = BoolValue(GetValue(op.Left, ctx));
    return TypedValue.MakeBool(op.Type switch
    {
      ValOpType.And when !left => false,
      ValOpType.Or when left => true,
      ValOpType.And or ValOpType.Or => BoolValue(GetValue(op.Right, ctx)),
      _ => throw new InvalidOperationException($"{op.Type}"),
    });
  }

  private TypedValue CompareOp(int idx, Nav ctx)
  {
    ref readonly var op = ref Path.Vals[idx];
    var left = GetValue(op.Left, ctx);
    var right = GetValue(op.Right, ctx);

    bool res;

    if (left.Type is ValueType.NodeSet && right.Type is ValueType.NodeSet)
    {
      var (lidx, ridx) = (left.Value.NodeSet, right.Value.NodeSet);
      if (op.Type is ValOpType.Eq or ValOpType.Neq)
        res = NodeSetsEqual(lidx, ridx, op.Type is ValOpType.Eq);
      else
      {
        var (minCmp, maxCmp) = ValOpCompareRange(op.Type);
        res = NodeSetsCompare(lidx, ridx, minCmp, maxCmp);
      }
    }
    else if (left.Type is ValueType.NodeSet || right.Type is ValueType.NodeSet)
    {
      var swap = right.Type is ValueType.NodeSet;
      if (swap)
        (left, right) = (right, left);

      if (op.Type is ValOpType.Eq or ValOpType.Neq)
        res = NodeSetValEqual(left.Value.NodeSet, right, op.Type is ValOpType.Eq);
      else
      {
        var (minCmp, maxCmp) = ValOpCompareRange(op.Type);
        if (swap)
          (minCmp, maxCmp) = (-maxCmp, -minCmp);
        res = NodeSetValCompare(left.Value.NodeSet, right, minCmp, maxCmp);
      }
    }
    else if (op.Type is ValOpType.Eq or ValOpType.Neq)
    {
      res = ValsEqual(left, right) == op.Type is ValOpType.Eq;
    }
    else
    {
      var (minCmp, maxCmp) = ValOpCompareRange(op.Type);
      var cmp = ValsCompare(left, right);
      res = cmp >= minCmp && cmp <= maxCmp;
    }

    return TypedValue.MakeBool(res);
  }

  private static (int, int) ValOpCompareRange(ValOpType type) => type switch
  {
    ValOpType.Lt => (-1, -1),
    ValOpType.Lte => (-1, 0),
    ValOpType.Gt => (1, 1),
    ValOpType.Gte => (0, 1),
    _ => throw new InvalidOperationException($"{type}"),
  };

  private unsafe bool NodeSetsEqual(int lidx, int ridx, bool expected)
  {
    var lstart = stringBuf.Length;
    while (NextNode(lidx, out var nav))
      stringBuf.Add(AddNodeStringValue(nav));
    var rstart = stringBuf.Length;
    while (NextNode(ridx, out var nav))
      stringBuf.Add(AddNodeStringValue(nav));
    var end = stringBuf.Length;

    var lstrs = stringBuf[lstart..rstart];
    var rstrs = stringBuf[rstart..end];

    stringBuf.Length = lstart;

    fixed (char* cdata = &data.Span[0])
    {
      var cmp = new StringComparer(cdata);
      lstrs.Sort(cmp);
      rstrs.Sort(cmp);
    }

    lidx = ridx = 0;
    while (lidx < lstrs.Length && ridx < rstrs.Length)
    {
      var cmp = String(lstrs[lidx]).SequenceCompareTo(String(rstrs[ridx]));
      if (cmp < 0)
        lidx++;
      else if (cmp > 0)
        ridx++;
      else
        return true;
    }
    return false;
  }

  // unsafe hacks to get around ref of ref struct restrictions
  private readonly unsafe struct StringComparer(char* data) : IComparer<Range>
  {
    private readonly char* data = data;

    public int Compare(Range x, Range y) => String(x).SequenceCompareTo(String(y));

    private ReadOnlySpan<char> String(Range range)
    {
      if (range.Start.IsFromEnd)
        return DEFAULT_DATA.AsSpan()[range.Start.Value..range.End.Value];
      return new Span<char>(data, range.End.Value)[range];
    }
  }

  private bool NodeSetsCompare(int lidx, int ridx, int minCmp, int maxCmp)
  {
    var less = minCmp < 0;

    if (!NextNode(lidx, out var nav))
      return false;
    var lbound = NumberValue(nav);
    while (NextNode(lidx, out nav))
    {
      var val = NumberValue(nav);
      lbound = less ? Math.Min(lbound, val) : Math.Max(lbound, val);
    }

    while (NextNode(ridx, out nav))
    {
      var cmp = lbound.CompareTo(NumberValue(nav));
      if (cmp >= minCmp && cmp <= maxCmp)
        return true;
    }
    return false;
  }

  private bool NodeSetValEqual(int nodes, TypedValue val, bool expected)
  {
    while (NextNode(nodes, out var nav))
    {
      var nval = TypedValue.MakeString(data.AddRest(nav.StringValue(data.Rest)));
      if (ValsEqual(nval, val) == expected)
        return true;
    }
    return false;
  }

  private bool NodeSetValCompare(int nodes, TypedValue val, int minCmp, int maxCmp)
  {
    var right = NumberValue(val);
    while (NextNode(nodes, out var nav))
    {
      var left = NumberValue(nav);
      var cmp = left.CompareTo(right);
      if (cmp >= minCmp && cmp <= maxCmp)
        return true;
    }
    return false;
  }

  private bool ValsEqual(TypedValue left, TypedValue right)
  {
    if (left.Type is ValueType.Bool || right.Type is ValueType.Bool)
      return BoolValue(left) == BoolValue(right);
    else if (left.Type is ValueType.Number || right.Type is ValueType.Number)
      return NumberValue(left) == NumberValue(right);
    else if (left.Type is ValueType.String && right.Type is ValueType.String)
      return String(StringValue(left)).SequenceEqual(String(StringValue(right)));
    else
      throw new InvalidOperationException($"{left.Type} {right.Type}");
  }

  private int ValsCompare(TypedValue left, TypedValue right) =>
    NumberValue(left).CompareTo(NumberValue(right));

  private TypedValue MathOp(int idx, Nav ctx)
  {
    ref readonly var op = ref Path.Vals[idx];
    var left = NumberValue(GetValue(op.Left, ctx));
    var right = NumberValue(GetValue(op.Right, ctx));
    return TypedValue.MakeNumber(op.Type switch
    {
      ValOpType.Add => left + right,
      ValOpType.Sub => left - right,
      ValOpType.Mult => left * right,
      ValOpType.Mod => left % right,
      ValOpType.Div => left / right,
      _ => throw new InvalidOperationException($"{op.Type}"),
    });
  }

  public bool BoolValue(TypedValue val) => val.Type switch
  {
    ValueType.Bool => val.Value.Bool,
    ValueType.Number => val.Value.Number != 0 && !double.IsNaN(val.Value.Number),
    ValueType.String => val.Value.String.End.Value > val.Value.String.Start.Value,
    ValueType.NodeSet => NextNode(val.Value.NodeSet, out _),
    _ => throw new InvalidOperationException($"{val.Type}"),
  };

  public double NumberValue(TypedValue val) => val.Type switch
  {
    ValueType.Bool => val.Value.Bool ? 1 : 0,
    ValueType.Number => val.Value.Number,
    ValueType.String => double.TryParse(
      String(val.Value.String),
      NumberStyles.Integer | NumberStyles.AllowDecimalPoint,
      null,
      out double parsed
    ) ? parsed : double.NaN,
    ValueType.NodeSet => NumberValue(TypedValue.MakeString(AddNodeStringValue(val.Value.NodeSet))),
    _ => throw new InvalidOperationException($"{val.Type}"),
  };

  public Range StringValue(TypedValue val) => val.Type switch
  {
    ValueType.Bool => val.Value.Bool ? TRUE_DATA : FALSE_DATA,
    ValueType.Number => GetNumberStringValue(val.Value.Number),
    ValueType.String => val.Value.String,
    ValueType.NodeSet => AddNodeStringValue(val.Value.NodeSet),
    _ => throw new InvalidOperationException($"{val.Type}"),
  };

  private Range GetNumberStringValue(double num)
  {
    if (double.IsNaN(num))
      return NAN_DATA;
    if (num == 0 || num == -0)
      return ZERO_DATA;
    if (double.IsPositiveInfinity(num))
      return PINF_DATA;
    if (double.IsNegativeInfinity(num))
      return NINF_DATA;

    if (!num.TryFormat(data.Rest, out var len, "f"))
      return NAN_DATA;
    return data.AddRest(len);
  }

  private Range AddNodeStringValue(int idx)
  {
    if (!NextNode(idx, out var nav))
      return 0..0;
    return AddNodeStringValue(nav);
  }

  private Range AddNodeStringValue(Nav nav) => data.AddRest(nav.StringValue(data.Rest));

  private double NumberValue(Nav nav)
  {
    // we don't need to retain the string, so just reset the data buffer after
    var start = data.Length;
    var num = NumberValue(TypedValue.MakeString(AddNodeStringValue(nav)));
    data.Length = start;
    return num;
  }
}