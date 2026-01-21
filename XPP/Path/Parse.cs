
using System;

namespace XPP.Path;

public class AstNode(AstType Type, Token Token, AstNode Left = null, AstNode Right = null)
{
  public readonly AstType Type = Type;
  public readonly Token Token = Token;
  public readonly AstNode Left = Left;
  public readonly AstNode Right = Right;
}

public class Parser
{
  public static AstNode Parse(string source)
  {
    var parser = new Parser(source);
    return parser.ParseExpr();
  }

  private readonly Tokenizer tokenizer;

  private bool hasNext = false;
  private Token next;

  private Parser(string source)
  {
    tokenizer = new(source);
  }

  private bool FillNext()
  {
    if (hasNext || tokenizer.EOF)
      return hasNext;

    hasNext = tokenizer.Next(out next);
    return hasNext;
  }

  private bool Peek(out Token tok)
  {
    FillNext();
    if (hasNext)
    {
      tok = next;
      return true;
    }
    tok = default;
    return false;
  }

  private bool PeekType(TokenType type) => FillNext() && next.Type == type;

  private bool PeekType(params ReadOnlySpan<TokenType> types)
  {
    if (!Peek(out var tok))
      return false;
    foreach (var type in types)
    {
      if (tok.Type == type)
        return true;
    }
    return false;
  }

  private bool PeekType(out Token tok, params ReadOnlySpan<TokenType> types)
  {
    if (!Peek(out tok))
      return false;
    foreach (var type in types)
    {
      if (tok.Type == type)
        return true;
    }
    return false;
  }

  private bool TakeType(TokenType type) => PeekType(type) && !(hasNext = false);

  private bool TakeType(out Token tok, TokenType type) =>
    Peek(out tok) && tok.Type == type && !(hasNext = false);

  private bool TakeType(out Token tok, params ReadOnlySpan<TokenType> types) =>
    PeekType(out tok, types) && !(hasNext = false);

  private bool TakeTypeRange(out Token tok, TokenType min, TokenType max) =>
    Peek(out tok) && tok.Type >= min && tok.Type <= max && !(hasNext = false);

  private Exception Invalid(TokenType expected)
  {
    Peek(out var tok);
    throw new InvalidOperationException($"{tok.String} {tok.Type} != {expected}");
  }

  private Exception Invalid(params TokenType[] expected)
  {
    Peek(out var tok);
    throw new InvalidOperationException($"{tok.String} {tok.Type} != {string.Join(',', expected)}");
  }

  private AstNode ParseLocationPath()
  {
    if (PeekType(TokenType.OpSep, TokenType.OpSepDesc))
      return ParseAbsoluteLocationPath();
    return ParseRelativeLocationPath();
  }

  // starts with OpSep or OpSepDesc(via AbbreviatedAbsoluteLocationPath)
  private AstNode ParseAbsoluteLocationPath()
  {
    if (!TakeType(out var tok, TokenType.OpSep, TokenType.OpSepDesc))
      throw Invalid(TokenType.OpSep, TokenType.OpSepDesc);
    return new(AstType.Root, tok, TryParseRelativeLocationPath());
  }

  private AstNode TryParseRelativeLocationPath()
  {
    if (!PeekType(TokenType.Self, TokenType.Parent, TokenType.AxisName, TokenType.Attr, TokenType.NtAny,
        TokenType.NtAnyNs, TokenType.NtName, TokenType.NodeType))
      return null;
    return ParseRelativeLocationPath();
  }

  // starts with Self|Parent(AbbreviatedStep) AxisName|Attr(AxisSpecifier) NameTest|NodeType(NodeTest)
  private AstNode ParseRelativeLocationPath()
  {
    // collapsed with AbbreviatedRelativeLocationPath
    var left = ParseStep();

    while (TakeType(out var tok, TokenType.OpSep, TokenType.OpSepDesc))
    {
      left = new(AstType.Sep, tok, left, ParseStep());
    }

    return left;
  }

  private AstNode ParseStep()
  {
    // AbbreviatedStep
    if (TakeType(out var tok, TokenType.Self, TokenType.Parent))
      return new(AstType.Axis, tok);

    // AxisSpecifier
    var hasAxis = TakeType(out var axis, TokenType.AxisName, TokenType.Attr);
    if (hasAxis && axis.Type == TokenType.AxisName && !TakeType(TokenType.Axis))
      throw Invalid(TokenType.Axis);

    var left = ParseNodeTest();
    if (hasAxis)
      left = new(AstType.Axis, axis, left);

    // Predicate*
    while (PeekType(TokenType.BOpen))
    {
      left = ParsePredicate(left, AstType.PathFilter);
    }

    return left;
  }

  private AstNode ParseNodeTest()
  {
    if (TakeTypeRange(out var tok, XPath.MinNt, XPath.MaxNt))
      return new(AstType.NodeTest, tok);

    if (!TakeType(out tok, TokenType.NodeType))
      throw Invalid(TokenType.NodeType);

    if (!TakeType(TokenType.POpen))
      throw Invalid(TokenType.POpen);

    var type = AstType.NodeTest;

    if (tok.String is "processing-instruction" && TakeType(out var ptype, TokenType.String))
    {
      type = AstType.ProcType;
      tok = ptype;
    }

    if (!TakeType(TokenType.PClose))
      throw Invalid(TokenType.PClose);

    return new(type, tok);
  }

  private AstNode ParsePredicate(AstNode left, AstType type)
  {
    if (!TakeType(out var tok, TokenType.BOpen))
      throw Invalid(TokenType.BOpen);

    var right = ParseExpr();

    if (!TakeType(TokenType.BClose))
      throw Invalid(TokenType.BClose);

    return new(type, tok, left, right);
  }

  private AstNode ParseExpr() => ParseOrExpr();

  private AstNode ParsePrimaryExpr()
  {
    if (TakeType(out var tok, TokenType.VarRef, TokenType.String, TokenType.Number))
      return new(AstType.Value, tok);

    if (TakeType(TokenType.POpen))
    {
      var res = ParseExpr();
      if (!TakeType(TokenType.PClose))
        throw Invalid(TokenType.PClose);
      return res;
    }

    if (!TakeType(out var fname, TokenType.FuncName))
      throw Invalid(TokenType.VarRef, TokenType.POpen, TokenType.String, TokenType.Number, TokenType.FuncName);

    if (!TakeType(TokenType.POpen))
      throw Invalid(TokenType.POpen);

    if (TakeType(TokenType.PClose))
      return new(AstType.FuncCall, fname);

    var args = ParseExpr();

    while (TakeType(out tok, TokenType.Comma))
    {
      var arg = ParseExpr();
      args = new(AstType.ArgList, tok, args, arg);
    }

    if (!TakeType(TokenType.PClose))
      throw Invalid(TokenType.PClose);

    return new(AstType.FuncCall, fname, args);
  }

  private AstNode ParseUnionExpr()
  {
    var left = ParsePathExpr();
    while (TakeType(out var tok, TokenType.OpUnion))
      left = new(AstType.Union, tok, left, ParsePathExpr());
    return left;
  }

  private AstNode ParsePathExpr()
  {
    if (!PeekType(TokenType.VarRef, TokenType.POpen, TokenType.String, TokenType.Number, TokenType.FuncName))
      return ParseLocationPath();

    var left = ParseFilterExpr();
    if (TakeType(out var tok, TokenType.OpSep, TokenType.OpSepDesc))
      left = new(AstType.Sep, tok, left, ParseRelativeLocationPath());
    return left;
  }

  private AstNode ParseFilterExpr()
  {
    var left = ParsePrimaryExpr();
    while (PeekType(TokenType.BOpen))
      left = ParsePredicate(left, AstType.ExprFilter);
    return left;
  }

  private AstNode ParseOrExpr()
  {
    var left = ParseAndExpr();
    while (TakeType(out var tok, TokenType.OpOr))
      left = new(AstType.BoolOp, tok, left, ParseAndExpr());
    return left;
  }

  private AstNode ParseAndExpr()
  {
    var left = ParseEqualityExpr();
    while (TakeType(out var tok, TokenType.OpAnd))
      left = new(AstType.BoolOp, tok, left, ParseEqualityExpr());
    return left;
  }

  private AstNode ParseEqualityExpr()
  {
    var left = ParseRelationalExpr();
    while (TakeType(out var tok, TokenType.OpEq, TokenType.OpNeq))
      left = new(AstType.CompareOp, tok, left, ParseRelationalExpr());
    return left;
  }

  private AstNode ParseRelationalExpr()
  {
    var left = ParseAdditiveExpr();
    while (TakeType(out var tok, TokenType.OpLt, TokenType.OpLte, TokenType.OpGt, TokenType.OpGte))
      left = new(AstType.CompareOp, tok, left, ParseAdditiveExpr());
    return left;
  }

  private AstNode ParseAdditiveExpr()
  {
    var left = ParseMultiplicativeExpr();
    while (TakeType(out var tok, TokenType.OpAdd, TokenType.OpSub))
      left = new(AstType.MathOp, tok, left, ParseMultiplicativeExpr());
    return left;
  }

  private AstNode ParseMultiplicativeExpr()
  {
    var left = ParseUnaryExpr();
    while (TakeType(out var tok, TokenType.OpMult, TokenType.OpDiv, TokenType.OpMod))
      left = new(AstType.MathOp, tok, left, ParseUnaryExpr());
    return left;
  }

  private AstNode ParseUnaryExpr()
  {
    if (TakeType(out var tok, TokenType.OpSub))
      return new(AstType.Negate, tok, ParseUnionExpr());
    return ParseUnionExpr();
  }
}