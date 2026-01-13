
using System;
using System.Globalization;
using XPP.Doc;

namespace XPP.Path;

public ref partial struct Exec
{
  private ref struct Value(XPValueType type)
  {
    public XPValueType Type = type;
    public bool Bool;
    public double Number;
    public string String;
    public int NodeSet;

    public Value(bool val) : this(XPValueType.Bool) => Bool = val;
    public Value(double val) : this(XPValueType.Number) => Number = val;
    public Value(string val) : this(XPValueType.String) => String = val;
    public Value(ReadOnlySpan<char> val) : this(XPValueType.String) => String = new(val);

    public static Value MakeNodeSet(int index) =>
      new(XPValueType.NodeSet) { NodeSet = index };
  }

  private Value GetValue(int idx, XPNavigator ctx)
  {
    ref readonly var op = ref path.Vals[idx];
    switch (op.Type)
    {
      case ValOpType.Number: return new(op.Value.Number);
      case ValOpType.String: return new(CompiledString(op.Value.String));
      case ValOpType.Variable:
        throw new NotImplementedException();
      case ValOpType.Path:
        ResetPath(op.Left, ctx);
        return Value.MakeNodeSet(op.Left);
      case ValOpType.Negate:
        return new(-NumberValue(GetValue(op.Left, ctx)));
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

  private Value BoolOp(int idx, XPNavigator ctx)
  {
    ref readonly var op = ref path.Vals[idx];
    var left = BoolValue(GetValue(op.Left, ctx));
    return new(op.Type switch
    {
      ValOpType.And when !left => false,
      ValOpType.Or when left => true,
      ValOpType.And or ValOpType.Or => BoolValue(GetValue(op.Right, ctx)),
      _ => throw new InvalidOperationException($"{op.Type}"),
    });
  }

  private Value CompareOp(int idx, XPNavigator ctx)
  {
    ref readonly var op = ref path.Vals[idx];
    var left = GetValue(op.Left, ctx);
    var right = GetValue(op.Right, ctx);

    bool res;

    if (left.Type is XPValueType.NodeSet && right.Type is XPValueType.NodeSet)
    {
      var (lidx, ridx) = (left.NodeSet, right.NodeSet);
      if (op.Type is ValOpType.Eq or ValOpType.Neq)
        res = NodeSetsEqual(lidx, ridx, op.Type is ValOpType.Eq);
      else
      {
        var (minCmp, maxCmp) = ValOpCompareRange(op.Type);
        res = NodeSetsCompare(lidx, ridx, minCmp, maxCmp);
      }
    }
    else if (left.Type is XPValueType.NodeSet || right.Type is XPValueType.NodeSet)
    {
      var swap = right.Type is XPValueType.NodeSet;
      if (swap)
      {
        var temp = left;
        left = right;
        right = temp;
      }

      if (op.Type is ValOpType.Eq or ValOpType.Neq)
        res = NodeSetValEqual(left.NodeSet, right, op.Type is ValOpType.Eq);
      else
      {
        var (minCmp, maxCmp) = ValOpCompareRange(op.Type);
        if (swap)
          (minCmp, maxCmp) = (-maxCmp, -minCmp);
        res = NodeSetValCompare(left.NodeSet, right, minCmp, maxCmp);
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

    return new(res);
  }

  private static (int, int) ValOpCompareRange(ValOpType type) => type switch
  {
    ValOpType.Lt => (-1, -1),
    ValOpType.Lte => (-1, 0),
    ValOpType.Gt => (1, 1),
    ValOpType.Gte => (0, 1),
    _ => throw new InvalidOperationException($"{type}"),
  };

  private bool NodeSetsEqual(int lidx, int ridx, bool expected)
  {
    var lstart = stringBuf.Length;
    while (NextNode(lidx, out var nav))
      stringBuf.Add(new(NodeStringValue(nav)));
    var rstart = stringBuf.Length;
    while (NextNode(ridx, out var nav))
      stringBuf.Add(new(NodeStringValue(nav)));
    var end = stringBuf.Length;

    var lstrs = stringBuf[lstart..rstart];
    var rstrs = stringBuf[rstart..end];

    stringBuf.Length = lstart;

    lstrs.Sort();
    rstrs.Sort();

    lidx = ridx = 0;
    while (lidx < lstrs.Length && ridx < rstrs.Length)
    {
      var cmp = lstrs[lidx].CompareTo(rstrs[ridx]);
      if (cmp < 0)
        lidx++;
      else if (cmp > 0)
        ridx++;
      else
        return true;
    }
    return false;
  }

  private bool NodeSetsCompare(int lidx, int ridx, int minCmp, int maxCmp)
  {
    var less = minCmp < 0;

    if (!NextNode(lidx, out var nav))
      return false;
    var lbound = NodeNumberValue(nav);
    while (NextNode(lidx, out nav))
    {
      var val = NodeNumberValue(nav);
      lbound = less ? Math.Min(lbound, val) : Math.Max(lbound, val);
    }

    while (NextNode(ridx, out nav))
    {
      var cmp = lbound.CompareTo(NodeNumberValue(nav));
      if (cmp >= minCmp && cmp <= maxCmp)
        return true;
    }
    return false;
  }

  private bool NodeSetValEqual(int nodes, Value val, bool expected)
  {
    while (NextNode(nodes, out var nav))
    {
      if (ValsEqual(new(NodeStringValue(nav)), val) == expected)
        return true;
    }
    return false;
  }

  private bool NodeSetValCompare(int nodes, Value val, int minCmp, int maxCmp)
  {
    var right = NumberValue(val);
    while (NextNode(nodes, out var nav))
    {
      var left = NodeNumberValue(nav);
      var cmp = left.CompareTo(right);
      if (cmp >= minCmp && cmp <= maxCmp)
        return true;
    }
    return false;
  }

  private bool ValsEqual(Value left, Value right)
  {
    if (left.Type is XPValueType.Bool || right.Type is XPValueType.Bool)
      return BoolValue(left) == BoolValue(right);
    else if (left.Type is XPValueType.Number || right.Type is XPValueType.Number)
      return NumberValue(left) == NumberValue(right);
    else if (left.Type is XPValueType.String && right.Type is XPValueType.String)
      return left.String == right.String;
    else
      throw new InvalidOperationException($"{left.Type} {right.Type}");
  }

  private int ValsCompare(Value left, Value right) =>
    NumberValue(left).CompareTo(NumberValue(right));

  private Value MathOp(int idx, XPNavigator ctx)
  {
    ref readonly var op = ref path.Vals[idx];
    var left = NumberValue(GetValue(op.Left, ctx));
    var right = NumberValue(GetValue(op.Right, ctx));
    return new(op.Type switch
    {
      ValOpType.Add => left + right,
      ValOpType.Sub => left - right,
      ValOpType.Mult => left * right,
      ValOpType.Mod => left % right,
      ValOpType.Div => left / right,
      _ => throw new InvalidOperationException($"{op.Type}"),
    });
  }

  private bool BoolValue(Value val) => val.Type switch
  {
    XPValueType.Bool => val.Bool,
    XPValueType.Number => val.Number != 0 && !double.IsNaN(val.Number),
    XPValueType.String => val.String.Length > 0,
    XPValueType.NodeSet => NextNode(val.NodeSet, out _),
    _ => throw new InvalidOperationException($"{val.Type}"),
  };

  private double NumberValue(Value val) => val.Type switch
  {
    XPValueType.Bool => val.Bool ? 1 : 0,
    XPValueType.Number => val.Number,
    XPValueType.String => StringNumberValue(val.String),
    XPValueType.NodeSet => NodeSetNumberValue(val.NodeSet),
    _ => throw new InvalidOperationException($"{val.Type}"),
  };

  private double NodeSetNumberValue(int nodeSet) =>
    StringNumberValue(NodeSetStringValue(nodeSet));

  private double NodeNumberValue(XPNavigator nav) =>
    StringNumberValue(NodeStringValue(nav));

  private double StringNumberValue(ReadOnlySpan<char> val) =>
    double.TryParse(
      val,
      NumberStyles.Integer | NumberStyles.AllowDecimalPoint,
      null,
      out double parsed
    ) ? parsed : double.NaN;

  private string StringValue(Value val) => val.Type switch
  {
    XPValueType.Bool => val.Bool ? TRUE : FALSE,
    XPValueType.Number => NumberStringValue(val.Number),
    XPValueType.String => val.String,
    XPValueType.NodeSet => new(NodeSetStringValue(val.NodeSet)),
    _ => throw new InvalidOperationException($"{val.Type}"),
  };

  public static string BoolStringValue(bool val) => val ? TRUE : FALSE;
  public static string NumberStringValue(double num)
  {
    if (double.IsNaN(num))
      return NAN;
    if (num == 0 || num == -0)
      return ZERO;
    if (double.IsPositiveInfinity(num))
      return PINF;
    if (double.IsNegativeInfinity(num))
      return NINF;

    return $"{num:f}";
  }

  private ReadOnlySpan<char> NodeSetStringValue(int idx) =>
    NextNode(idx, out var nav) ? NodeStringValue(nav) : [];

  private ReadOnlySpan<char> NodeStringValue(XPNavigator nav) =>
    dataBuf[..nav.StringValue(dataBuf)];
}