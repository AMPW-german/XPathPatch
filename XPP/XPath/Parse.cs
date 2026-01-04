
using System;

namespace XPP.XPatch;

public partial class XPath
{
  public enum AstType
  {
    Invalid = -1,
    Unset = 0,

    Root, // Tok:OpSep|OpSepDesc 0:RelativeLocationPath?
    Sep, // 0:Step Tok:OpSep|OpSepDesc 1:RelativeLocationPath
    Axis, // (Tok:AxisName OpAxis ...) | (Tok:Attr ...) | (Tok:Self|Parent)
    NodeTest, // Tok:NameTest | (Tok:NodeType POpen PClose)
    ProcType, // 'processing-instruction' POpen Tok:String PClose
    Filter, // 0:Expr Tok:BOpen 1:Expr BClose
    Value, // Tok:VarRef|String|Number
    FuncCall, // Tok:FuncName POpen 0:(ArgList|Expr)? PClose
    ArgList, // 0:ArgList|Expr Tok:Comma 1:Expr

    // Binary ops = 0:Expr Tok:Op 1:Expr
    Union, // OpUnion
    BoolOp, // OpAnd OpOr
    CompareOp, // OpEq OpNeq OpLt OpLte OpGt OpGte
    MathOp, // OpAdd OpSub OpMult OpMod OpDiv

    Negate, // Tok:OpSub 0:Expr
  }

  public readonly struct AstNode(AstType Type, Token Token, int Child0 = -1, int Child1 = -1)
  {
    public readonly AstType Type = Type;
    public readonly Token Token = Token;
    public readonly int Child0 = Child0;
    public readonly int Child1 = Child1;
  }

  public ref struct Parser
  {
    public static ReadOnlySpan<AstNode> Parse(string source)
    {
      var parser = new Parser(source);
      parser.ParseExpr();

      return parser.nodes[..parser.length];
    }

    [ThreadStatic]
    private static AstNode[] nodeBuffer;

    private readonly ReadOnlySpan<char> fullSource;
    private Tokenizer tokenizer;
    private readonly Span<AstNode> nodes;
    private int length = 0;

    private bool hasNext = false;
    private Token next;

    private Parser(ReadOnlySpan<char> source)
    {
      fullSource = source;
      tokenizer = new(source);
      nodes = nodeBuffer ??= new AstNode[MAX_LENGTH];
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

    private bool PeekType(out Token tok, TokenType type) => Peek(out tok) && tok.Type == type;

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

    private bool PeekTypeStr(out Token tok, TokenType type, params ReadOnlySpan<string> strs)
    {
      if (!PeekType(out tok, type))
        return false;
      var tstr = TokStr(tok);
      foreach (var str in strs)
      {
        if (tstr == str)
          return true;
      }
      return false;
    }

    private bool Take(out Token tok) => Peek(out tok) && !(hasNext = false);

    private void Take()
    {
      if (!hasNext) throw new InvalidOperationException();
      hasNext = false;
    }

    private bool TakeType(TokenType type) => PeekType(type) && !(hasNext = false);

    private bool TakeType(out Token tok, TokenType type) =>
      Peek(out tok) && tok.Type == type && !(hasNext = false);

    private bool TakeType(out Token tok, params ReadOnlySpan<TokenType> types) =>
      PeekType(out tok, types) && !(hasNext = false);

    private bool TakeTypeRange(out Token tok, TokenType min, TokenType max) =>
      Peek(out tok) && tok.Type >= min && tok.Type <= max && !(hasNext = false);

    private bool TakeTypeStr(out Token tok, TokenType type, string str)
    {
      if (!PeekType(out tok, type) || TokStr(tok) != str)
        return false;
      hasNext = false;
      return true;
    }

    private bool TakeTypeStr(out Token tok, TokenType type, params ReadOnlySpan<string> strs) =>
      PeekTypeStr(out tok, type, strs) && !(hasNext = false);

    private ReadOnlySpan<char> TokStr(Token tok) => fullSource[tok.Data];

    private Exception Invalid(string msg = null) => throw new InvalidOperationException(msg);

    private Exception Invalid(TokenType expected) => throw new InvalidOperationException($"{expected}");

    private Exception Invalid(params TokenType[] expected) =>
      throw new InvalidOperationException($"{string.Join(',', expected)}");

    private int Push(AstNode node)
    {
      if (length == nodes.Length)
        throw new IndexOutOfRangeException();
      nodes[length] = node;
      return length++;
    }

    private int ParseLocationPath()
    {
      if (PeekType(TokenType.OpSep, TokenType.OpSepDesc))
        return ParseAbsoluteLocationPath();
      return ParseRelativeLocationPath();
    }

    // starts with OpSep or OpSepDesc(via AbbreviatedAbsoluteLocationPath)
    private int ParseAbsoluteLocationPath()
    {
      if (!TakeType(out var tok, TokenType.OpSep, TokenType.OpSepDesc))
        throw Invalid(TokenType.OpSep, TokenType.OpSepDesc);
      return Push(new(AstType.Root, tok, TryParseRelativeLocationPath()));
    }

    private int TryParseRelativeLocationPath()
    {
      if (!PeekType(TokenType.Self, TokenType.Parent, TokenType.AxisName, TokenType.Attr, TokenType.NtAny,
          TokenType.NtAnyNs, TokenType.NtName, TokenType.NodeType))
        return -1;
      return ParseRelativeLocationPath();
    }

    // starts with Self|Parent(AbbreviatedStep) AxisName|Attr(AxisSpecifier) NameTest|NodeType(NodeTest)
    private int ParseRelativeLocationPath()
    {
      // collapsed with AbbreviatedRelativeLocationPath
      var left = ParseStep();

      while (TakeType(out var tok, TokenType.OpSep, TokenType.OpSepDesc))
      {
        left = Push(new(AstType.Sep, tok, left, ParseStep()));
      }

      return left;
    }

    private int ParseStep()
    {
      // AbbreviatedStep
      if (TakeType(out var tok, TokenType.Self, TokenType.Parent))
        return Push(new(AstType.Axis, tok));

      // AxisSpecifier
      var hasAxis = TakeType(out var axis, TokenType.AxisName, TokenType.Attr);
      if (hasAxis && axis.Type == TokenType.AxisName && !TakeType(TokenType.Axis))
        throw Invalid(TokenType.Axis);

      var left = ParseNodeTest();
      if (hasAxis)
        left = Push(new(AstType.Axis, axis, left));

      // Predicate*
      while (PeekType(TokenType.BOpen))
      {
        left = ParsePredicate(left);
      }

      return left;
    }

    private int ParseNodeTest()
    {
      if (TakeTypeRange(out var tok, MinNt, MaxNt))
        return Push(new(AstType.NodeTest, tok));

      if (!TakeType(out tok, TokenType.NodeType))
        throw Invalid(TokenType.NodeType);

      if (!TakeType(TokenType.POpen))
        throw Invalid(TokenType.POpen);

      var type = AstType.NodeTest;

      if (TokStr(tok) is "processing-instruction" && TakeType(out var ptype, TokenType.String))
      {
        type = AstType.ProcType;
        tok = ptype;
      }

      if (!TakeType(TokenType.PClose))
        throw Invalid(TokenType.PClose);

      return Push(new(type, tok));
    }

    private int ParsePredicate(int left)
    {
      if (!TakeType(out var tok, TokenType.BOpen))
        throw Invalid(TokenType.BOpen);

      var right = ParseExpr();

      if (!TakeType(TokenType.BClose))
        throw Invalid(TokenType.BClose);

      return Push(new(AstType.Filter, tok, left, right));
    }

    private int ParseExpr() => ParseOrExpr();

    private int ParsePrimaryExpr()
    {
      if (TakeType(out var tok, TokenType.VarRef, TokenType.String, TokenType.Number))
        return Push(new(AstType.Value, tok));

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
        return Push(new(AstType.FuncCall, fname));

      var args = ParseExpr();

      while (TakeType(out tok, TokenType.Comma))
      {
        var arg = ParseExpr();
        args = Push(new(AstType.ArgList, tok, args, arg));
      }

      if (!TakeType(TokenType.PClose))
        throw Invalid(TokenType.PClose);

      return Push(new(AstType.FuncCall, fname, args));
    }

    private int ParseUnionExpr()
    {
      var left = ParsePathExpr();
      while (TakeType(out var tok, TokenType.OpUnion))
        left = Push(new(AstType.Union, tok, left, ParsePathExpr()));
      return left;
    }

    private int ParsePathExpr()
    {
      if (!PeekType(TokenType.VarRef, TokenType.POpen, TokenType.String, TokenType.Number, TokenType.FuncName))
        return ParseLocationPath();

      var left = ParseFilterExpr();
      if (TakeType(out var tok, TokenType.OpSep, TokenType.OpSepDesc))
        left = Push(new(AstType.Sep, tok, left, ParseRelativeLocationPath()));
      return left;
    }

    private int ParseFilterExpr()
    {
      var left = ParsePrimaryExpr();
      while (PeekType(TokenType.BOpen))
        left = ParsePredicate(left);
      return left;
    }

    private int ParseOrExpr()
    {
      var left = ParseAndExpr();
      while (TakeType(out var tok, TokenType.OpOr))
        left = Push(new(AstType.BoolOp, tok, left, ParseAndExpr()));
      return left;
    }

    private int ParseAndExpr()
    {
      var left = ParseEqualityExpr();
      while (TakeType(out var tok, TokenType.OpAnd))
        left = Push(new(AstType.BoolOp, tok, left, ParseEqualityExpr()));
      return left;
    }

    private int ParseEqualityExpr()
    {
      var left = ParseRelationalExpr();
      while (TakeType(out var tok, TokenType.OpEq, TokenType.OpNeq))
        left = Push(new(AstType.CompareOp, tok, left, ParseRelationalExpr()));
      return left;
    }

    private int ParseRelationalExpr()
    {
      var left = ParseAdditiveExpr();
      while (TakeType(out var tok, TokenType.OpLt, TokenType.OpLte, TokenType.OpGt, TokenType.OpGte))
        left = Push(new(AstType.CompareOp, tok, left, ParseAdditiveExpr()));
      return left;
    }

    private int ParseAdditiveExpr()
    {
      var left = ParseMultiplicativeExpr();
      while (TakeType(out var tok, TokenType.OpAdd, TokenType.OpSub))
        left = Push(new(AstType.MathOp, tok, left, ParseMultiplicativeExpr()));
      return left;
    }

    private int ParseMultiplicativeExpr()
    {
      var left = ParseUnaryExpr();
      while (TakeType(out var tok, TokenType.OpMult, TokenType.OpDiv, TokenType.OpMod))
        left = Push(new(AstType.MathOp, tok, left, ParseUnaryExpr()));
      return left;
    }

    private int ParseUnaryExpr()
    {
      if (TakeType(out var tok, TokenType.OpSub))
        return Push(new(AstType.Negate, tok, ParseUnionExpr()));
      return ParseUnionExpr();
    }
  }
}