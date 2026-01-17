
using System;
using XPP.Doc;
using XPP.Utils;

namespace XPP.Path;

public ref partial struct Exec
{
  private Value FuncValue(int idx, PathContext ctx)
  {
    ref readonly var op = ref path.Vals[idx];
    var (off, len) = op.Args.GetOffsetAndLength(path.Vals.Length);
    using var argb = argBuf.Borrow(len);
    var args = argb.Span;
    for (var i = 0; i < len; i++)
      args[i] = GetValue(off + i, ctx);
    return op.Func switch
    {
      LibraryFunc.Last => LibraryFuncLast(args, ctx),
      LibraryFunc.Position => LibraryFuncPosition(args, ctx),
      LibraryFunc.Count => LibraryFuncCount(args, ctx),
      LibraryFunc.Id => LibraryFuncId(args, ctx),
      LibraryFunc.LocalName => LibraryFuncLocalName(args, ctx),
      LibraryFunc.NamespaceUri => LibraryFuncNamespaceUri(args, ctx),
      LibraryFunc.Name => LibraryFuncName(args, ctx),
      LibraryFunc.String => LibraryFuncString(args, ctx),
      LibraryFunc.Concat => LibraryFuncConcat(args, ctx),
      LibraryFunc.StartsWith => LibraryFuncStartsWith(args, ctx),
      LibraryFunc.Contains => LibraryFuncContains(args, ctx),
      LibraryFunc.SubstringBefore => LibraryFuncSubstringBefore(args, ctx),
      LibraryFunc.SubstringAfter => LibraryFuncSubstringAfter(args, ctx),
      LibraryFunc.Substring => LibraryFuncSubstring(args, ctx),
      LibraryFunc.StringLength => LibraryFuncStringLength(args, ctx),
      LibraryFunc.NormalizeSpace => LibraryFuncNormalizeSpace(args, ctx),
      LibraryFunc.Translate => LibraryFuncTranslate(args, ctx),
      LibraryFunc.Boolean => LibraryFuncBoolean(args, ctx),
      LibraryFunc.Not => LibraryFuncNot(args, ctx),
      LibraryFunc.True => LibraryFuncTrue(args, ctx),
      LibraryFunc.False => LibraryFuncFalse(args, ctx),
      LibraryFunc.Lang => LibraryFuncLang(args, ctx),
      LibraryFunc.Number => LibraryFuncNumber(args, ctx),
      LibraryFunc.Sum => LibraryFuncSum(args, ctx),
      LibraryFunc.Floor => LibraryFuncFloor(args, ctx),
      LibraryFunc.Ceiling => LibraryFuncCeiling(args, ctx),
      LibraryFunc.Round => LibraryFuncRound(args, ctx),
      _ => throw new InvalidOperationException($"{op.Func}"),
    };
  }

  private static void AssertArgType(
    string fname, int argi, XPValueType expected, ref readonly Value val)
  {
    if (val.Type == expected)
      return;
    throw new InvalidOperationException(
      $"argument {argi} to {fname} must be {expected}, not {val.Type}");
  }

  private Value LibraryFuncLast(scoped Span<Value> args, PathContext ctx) =>
    new(ctx.Count);
  private Value LibraryFuncPosition(scoped Span<Value> args, PathContext ctx) =>
    new(ctx.Position + 1);

  private Value LibraryFuncCount(scoped Span<Value> args, PathContext ctx)
  {
    AssertArgType("count", 0, XPValueType.NodeSet, in args[0]);

    return new(PathResult(args[0].NodeSet).Length);
  }

  private Value LibraryFuncId(scoped Span<Value> args, PathContext ctx) =>
    throw new NotImplementedException();

  private Value LibraryFuncLocalName(scoped Span<Value> args, PathContext ctx)
  {
    var node = ctx.Nav.Node;
    if (args.Length > 0)
    {
      AssertArgType("local-name", 0, XPValueType.NodeSet, in args[0]);
      var nodes = PathResult(args[0].NodeSet);
      node = nodes.Length > 0 ? nodes[0].Nav.Node : XPNodeRef.Invalid;
    }
    return new(node.Name.Local);
  }

  private Value LibraryFuncNamespaceUri(scoped Span<Value> args, PathContext ctx)
  {
    var node = ctx.Nav.Node;
    if (args.Length > 0)
    {
      AssertArgType("namespace-uri", 0, XPValueType.NodeSet, in args[0]);
      var nodes = PathResult(args[0].NodeSet);
      node = nodes.Length > 0 ? nodes[0].Nav.Node : XPNodeRef.Invalid;
    }
    return new(node.Name.NsUri);
  }

  private Value LibraryFuncName(scoped Span<Value> args, PathContext ctx)
  {
    var node = ctx.Nav.Node;
    if (args.Length > 0)
    {
      AssertArgType("name", 0, XPValueType.NodeSet, in args[0]);
      var nodes = PathResult(args[0].NodeSet);
      node = nodes.Length > 0 ? nodes[0].Nav.Node : XPNodeRef.Invalid;
    }
    var name = node.Name;
    if (name.Prefix.Length > 0)
      return new($"{name.Prefix}:{name.Local}");
    return new(name.Local);
  }

  private Value LibraryFuncString(scoped Span<Value> args, PathContext ctx)
  {
    if (args.Length > 0)
      return new(StringValue(args[0]));
    return new(NodeStringValue(ctx.Nav));
  }

  private Value LibraryFuncConcat(scoped Span<Value> args, PathContext ctx) =>
    throw new NotImplementedException();
  private Value LibraryFuncStartsWith(scoped Span<Value> args, PathContext ctx) =>
    new(StringValue(args[0]).StartsWith(StringValue(args[1])));
  private Value LibraryFuncContains(scoped Span<Value> args, PathContext ctx) =>
    new(StringValue(args[0]).Contains(StringValue(args[1])));

  private Value LibraryFuncSubstringBefore(scoped Span<Value> args, PathContext ctx)
  {
    var arg0 = StringValue(args[0]);
    var idx = arg0.IndexOf(StringValue(args[1]));
    return new(idx == -1 ? "" : arg0[..idx]);
  }

  private Value LibraryFuncSubstringAfter(scoped Span<Value> args, PathContext ctx)
  {
    var arg0 = StringValue(args[0]);
    var arg1 = StringValue(args[1]);
    var idx = arg0.IndexOf(arg1);
    return new(idx == -1 ? "" : arg0[(idx + arg1.Length)..]);
  }

  private Value LibraryFuncSubstring(scoped Span<Value> args, PathContext ctx)
  {
    var str = StringValue(args[0]);
    var min = (long)(int)Math.Round(NumberValue(args[1]) - 1);
    var len = args.Length > 2 ? (long)(int)Math.Round(NumberValue(args[1])) : str.Length;

    var imin = (int)Math.Clamp(min, 0, str.Length);
    var imax = (int)Math.Clamp(min + len, 0, str.Length);
    if (imin >= imax)
      return new("");
    return new(str[imin..imax]);
  }

  private Value LibraryFuncStringLength(scoped Span<Value> args, PathContext ctx)
  {
    if (args.Length > 0)
      return new(StringValue(args[0]).Length);
    return new(NodeStringValue(ctx.Nav).Length);
  }

  private Value LibraryFuncNormalizeSpace(scoped Span<Value> args, PathContext ctx)
  {
    var str = args.Length > 0 ? StringValue(args[0]) : NodeStringValue(ctx.Nav);

    var len = 0;
    var nslen = 0;
    var hassp = true;
    for (var i = 0; i < str.Length; i++)
    {
      var c = str[i];
      var hadsp = hassp;
      if (hassp = char.IsWhiteSpace(c))
      {
        if (hadsp) continue;
        c = ' ';
      }
      dataBuf[len++] = c;
      if (!hassp) nslen = len;
    }
    return new(dataBuf[..nslen]);
  }

  private Value LibraryFuncTranslate(scoped Span<Value> args, PathContext ctx) =>
    throw new NotSupportedException("translate not supported");
  private Value LibraryFuncBoolean(scoped Span<Value> args, PathContext ctx) =>
    new(BoolValue(args[0]));
  private Value LibraryFuncNot(scoped Span<Value> args, PathContext ctx) =>
    new(!BoolValue(args[0]));
  private Value LibraryFuncTrue(scoped Span<Value> args, PathContext ctx) => new(true);
  private Value LibraryFuncFalse(scoped Span<Value> args, PathContext ctx) => new(false);
  private Value LibraryFuncLang(scoped Span<Value> args, PathContext ctx) =>
    throw new NotSupportedException("lang not supported");

  private Value LibraryFuncNumber(scoped Span<Value> args, PathContext ctx)
  {
    if (args.Length > 0)
      return new(NumberValue(args[0]));
    return new(NodeNumberValue(ctx.Nav));
  }

  private Value LibraryFuncSum(scoped Span<Value> args, PathContext ctx)
  {
    AssertArgType("sum", 0, XPValueType.NodeSet, in args[0]);
    var sum = 0.0;
    foreach (var node in PathResult(args[0].NodeSet))
      sum += NodeNumberValue(node.Nav);
    return new(sum);
  }

  private Value LibraryFuncFloor(scoped Span<Value> args, PathContext ctx) =>
    new(Math.Floor(NumberValue(args[0])));
  private Value LibraryFuncCeiling(scoped Span<Value> args, PathContext ctx) =>
    new(Math.Ceiling(NumberValue(args[0])));
  private Value LibraryFuncRound(scoped Span<Value> args, PathContext ctx) =>
    new(Math.Round(NumberValue(args[0])));
}