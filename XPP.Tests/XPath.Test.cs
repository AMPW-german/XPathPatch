using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using XPP.Path;

namespace XPP.Tests;

[TestClass]
public partial class XPathTests : BaseTest;

public class XPathEntry
{
  [XmlAttribute("Expr")] public string Expr;
  [XmlArray("Tokenize"), XmlArrayItem("Tok")] public List<XPathTok> Tokens;
  [XmlElement("Parse")] public XPathAst Parsed;
  [XmlElement("Compile")] public XPathCExpr Compiled;

  public override string ToString() => Expr;
}

public partial class XPathTok
{
  [XmlAttribute("T")] public TokenType Type;
  [XmlAttribute("V")] public string Value;
}

public partial class XPathAst
{
  [XmlAttribute("T")] public AstType Type;
  [XmlAttribute("V")] public string Value;
  [XmlElement("L")] public XPathAst Left;
  [XmlElement("R")] public XPathAst Right;
}

public partial class XPathCExpr
{
  [XmlAttribute("T")] public ValOpType Type;
  [XmlAttribute("Str")] public string String;
  [XmlAttribute("Num")]
  public double _Number
  { get => Number ?? 0; set => Number = value; }
  [XmlIgnore] public double? Number;
  [XmlAttribute("Fn")]
  public LibraryFunc _Func
  { get => Func ?? default; set => Func = value; }
  [XmlIgnore] public LibraryFunc? Func;
  [XmlElement("Path")] public List<XPathCPath> Path;
  [XmlElement("L")] public XPathCExpr Left;
  [XmlElement("R")] public XPathCExpr Right;
  [XmlElement("Arg")] public List<XPathCExpr> Args;
}

public partial class XPathCPath
{
  [XmlAttribute("T")] public PathOpType Type;
  [XmlIgnore] public PathOpMode Mode = PathOpMode.Linear;
  [XmlAttribute("Reo")]
  public bool _Reorder
  {
    get => Mode == PathOpMode.InsertExpand;
    set => Mode = value ? PathOpMode.InsertExpand : PathOpMode.Linear;
  }
  [XmlAttribute("Fwd")] public bool Forward = true;
  [XmlAttribute("Dd")] public bool Dedupe;
  [XmlAttribute("Rev")] public bool Reverse;
  [XmlArray("UL")]
  [XmlArrayItem("Path")]
  public List<XPathCPath> UnionL;
  [XmlArray("UR")]
  [XmlArrayItem("Path")]
  public List<XPathCPath> UnionR;
  [XmlAttribute("Ax")]
  public AxisType _Axis
  { get => Axis ?? default; set => Axis = value; }
  [XmlIgnore] public AxisType? Axis;
  [XmlAttribute("Nt")]
  public NodeType _NodeType
  { get => NodeType ?? default; set => NodeType = value; }
  [XmlIgnore] public NodeType? NodeType;
  [XmlAttribute("Ns")] public string Ns;
  [XmlAttribute("N")] public string Name;
  [XmlElement("Expr")] public XPathCExpr Expr;
}

public partial class XPathExecEntry
{
  [XmlAnyElement] public XmlElement Doc;
  [XmlElement("Test")] public List<XPathExecTest> Tests = [];
}

public partial class XPathExecTest
{
  [XmlAttribute("Path")] public string Path;
  [XmlElement("Match")] public List<XPathExecMatch> Matches = [];

  public override string ToString() => Path;
}

public partial class XPathExecMatch
{
  [XmlAttribute("Path")] public string Path;

  public static implicit operator BaseTest.Path(XPathExecMatch match) => match.Path;
}