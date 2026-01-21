
using System;
using System.Collections.Generic;
using XPP.Doc;

namespace XPP.Path;

public abstract class ExecExprOp()
{
  public abstract ExecResult Value(ExecPathCtx context);

  public ExecResult Evaluate(XPNodeRef node) => Value(new(node.Nav, ExecCtxSet.One));
}

public class ExecExprOpPath(ExecPathOp Path) : ExecExprOp()
{
  public readonly ExecPathOp Path = Path;

  public override ExecResult Value(ExecPathCtx context)
  {
    Path.Init(context.Nav);
    return new(Path);
  }
}

public class ExecExprOpConstant(ExecResult Val) : ExecExprOp()
{
  public readonly ExecResult Val = Val;
  public override ExecResult Value(ExecPathCtx context) => Val;
}

public class ExecExprOpNegate(ExecExprOp Base) : ExecExprOp()
{
  public readonly ExecExprOp Base = Base;

  public override ExecResult Value(ExecPathCtx context) =>
    new(-Base.Value(context).NumberValue);
}

public class ExecExprOpLogic(ExecExprOp Left, ExecExprOp Right, TokenType Op)
  : ExecExprOp()
{
  public readonly ExecExprOp Left = Left;
  public readonly ExecExprOp Right = Right;
  public readonly TokenType Op = Op;

  public override ExecResult Value(ExecPathCtx context)
  {
    var lval = Left.Value(context).BoolValue;
    var rval = Right.Value(context).BoolValue;

    return new(Op switch
    {
      TokenType.OpAnd => lval && rval,
      TokenType.OpOr => lval || rval,
      _ => throw new InvalidOperationException($"{Op}"),
    });
  }
}

public class ExecExprOpCompare(ExecExprOp Left, ExecExprOp Right, TokenType Op)
  : ExecExprOp()
{
  public readonly ExecExprOp Left = Left;
  public readonly ExecExprOp Right = Right;
  public readonly TokenType Op = Op;

  public override ExecResult Value(ExecPathCtx context)
  {
    var left = Left.Value(context);
    var right = Right.Value(context);

    bool res;
    if (left.Type is XPValueType.NodeSet && right.Type is XPValueType.NodeSet)
    {
      var (lidx, ridx) = (left.NodeSet, right.NodeSet);
      if (Op is TokenType.OpEq or TokenType.OpNeq)
        res = NodeSetsEqual(lidx, ridx, Op is TokenType.OpEq);
      else
      {
        var (minCmp, maxCmp) = ValOpCompareRange(Op);
        res = NodeSetsCompare(lidx, ridx, minCmp, maxCmp);
      }
    }
    else if (left.Type is XPValueType.NodeSet || right.Type is XPValueType.NodeSet)
    {
      var swap = right.Type is XPValueType.NodeSet;
      if (swap)
        (right, left) = (left, right);

      if (Op is TokenType.OpEq or TokenType.OpNeq)
        res = NodeSetValEqual(left.NodeSet, right, Op is TokenType.OpEq);
      else
      {
        var (minCmp, maxCmp) = ValOpCompareRange(Op);
        if (swap)
          (minCmp, maxCmp) = (-maxCmp, -minCmp);
        res = NodeSetValCompare(left.NodeSet, right, minCmp, maxCmp);
      }
    }
    else if (Op is TokenType.OpEq or TokenType.OpNeq)
      res = ValsEqual(left, right) == Op is TokenType.OpEq;
    else
    {
      var (minCmp, maxCmp) = ValOpCompareRange(Op);
      var cmp = ValsCompare(left, right);
      res = cmp >= minCmp && cmp <= maxCmp;
    }

    return new(res);
  }

  private static (int, int) ValOpCompareRange(TokenType type) => type switch
  {
    TokenType.OpLt => (-1, -1),
    TokenType.OpLte => (-1, 0),
    TokenType.OpGt => (1, 1),
    TokenType.OpGte => (0, 1),
    _ => throw new InvalidOperationException($"{type}"),
  };

  private List<string> lstrings;
  private List<string> rstrings;
  private bool NodeSetsEqual(ExecPathOp left, ExecPathOp right, bool expected)
  {
    lstrings ??= [];
    lstrings.Clear();
    foreach (var node in left)
      lstrings.Add(ExecValue.NodeStringValue(node));

    if (lstrings.Count == 0)
      return false;

    rstrings ??= [];
    rstrings.Clear();
    foreach (var node in right)
      rstrings.Add(ExecValue.NodeStringValue(node));

    if (rstrings.Count == 0)
    {
      lstrings.Clear();
      return false;
    }

    lstrings.Sort();
    rstrings.Sort();

    if (!expected)
    {
      var (lfirst, llast) = (lstrings[0], lstrings[^1]);
      var (rfirst, rlast) = (rstrings[0], rstrings[^1]);
      lstrings.Clear();
      rstrings.Clear();
      return lfirst != rfirst || lfirst != llast || rfirst != rlast;
    }

    var lidx = 0;
    var ridx = 0;
    var res = false;
    while (lidx < lstrings.Count && ridx < rstrings.Count)
    {
      var cmp = lstrings[lidx].CompareTo(rstrings[ridx]);
      if (cmp < 0)
        lidx++;
      else if (cmp > 0)
        ridx++;
      else
      {
        res = true;
        break;
      }
    }
    lstrings.Clear();
    rstrings.Clear();
    return res;
  }

  private static bool NodeSetsCompare(ExecPathOp left, ExecPathOp right, int minCmp, int maxCmp)
  {
    var less = minCmp < 0;
    var lbound = 0.0;
    var hasL = false;
    foreach (var node in left)
    {
      var val = ExecValue.NodeNumberValue(node);
      if (hasL)
        lbound = less ? Math.Min(lbound, val) : Math.Max(lbound, val);
      else
      {
        hasL = true;
        lbound = val;
      }
    }
    if (!hasL)
      return false;

    foreach (var node in right)
    {
      var cmp = lbound.CompareTo(ExecValue.NodeNumberValue(node));
      if (cmp >= minCmp && cmp <= maxCmp)
        return true;
    }
    return false;
  }

  private static bool NodeSetValEqual(ExecPathOp nodes, ExecResult val, bool expected)
  {
    foreach (var node in nodes)
    {
      if (ValsEqual(new(ExecValue.NodeStringValue(node)), val) == expected)
        return true;
    }
    return false;
  }

  private static bool NodeSetValCompare(ExecPathOp nodes, ExecResult val, int minCmp, int maxCmp)
  {
    var right = val.NumberValue;
    foreach (var node in nodes)
    {
      var left = ExecValue.NodeNumberValue(node);
      var cmp = left.CompareTo(right);
      if (cmp >= minCmp && cmp <= maxCmp)
        return true;
    }
    return false;
  }

  private static bool ValsEqual(ExecResult left, ExecResult right)
  {
    if (left.Type is XPValueType.Bool || right.Type is XPValueType.Bool)
      return left.BoolValue == right.BoolValue;
    else if (left.Type is XPValueType.Number || right.Type is XPValueType.Number)
      return left.NumberValue == right.NumberValue;
    else if (left.Type is XPValueType.String && right.Type is XPValueType.String)
      return left.String == right.String;
    else
      throw new InvalidOperationException($"{left.Type} {right.Type}");
  }

  private static int ValsCompare(ExecResult left, ExecResult right) =>
    left.NumberValue.CompareTo(right.NumberValue);
}

public class ExecExprOpMath(ExecExprOp Left, ExecExprOp Right, TokenType Op)
  : ExecExprOp()
{
  public readonly ExecExprOp Left = Left;
  public readonly ExecExprOp Right = Right;
  public readonly TokenType Op = Op;

  public override ExecResult Value(ExecPathCtx context)
  {
    var lval = Left.Value(context).NumberValue;
    var rval = Right.Value(context).NumberValue;
    return new(Op switch
    {
      TokenType.OpAdd => lval + rval,
      TokenType.OpSub => lval - rval,
      TokenType.OpMult => lval * rval,
      TokenType.OpDiv => lval / rval,
      TokenType.OpMod => lval % rval,
      _ => throw new InvalidOperationException($"{Op}"),
    });
  }
}