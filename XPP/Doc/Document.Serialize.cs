
using System;
using System.Text;

namespace XPP.Doc;

public partial class XPDocument
{
  public override string ToString() => ToString(int.MaxValue);

  public string ToString(int version = int.MaxValue)
  {
    var sb = new StringBuilder();
    AddNode(roots[^1], sb, "", version);
    return sb.ToString();
  }

  private void AddNode(
    int index, StringBuilder sb, string indent, int version, bool siblings = true)
  {
    while (index != -1)
    {
      ref var node = ref Latest(index, version);
      switch (node.Type)
      {
        case XPType.Document:
          AddNode(node.FirstContent, sb, indent, version);
          break;
        case XPType.Element:
          sb.Append(indent).Append('<');
          sb.AppendXPName(node.Name);
          AddNodeInline(node.FirstAttr, sb, version);
          if (node.FirstContent == -1)
          {
            sb.AppendLine(" />");
            break;
          }
          sb.AppendLine(">");
          AddNode(node.FirstContent, sb, indent + "  ", version);
          sb.Append(indent).Append("</").AppendXPName(node.Name).AppendLine(">");
          break;
        case XPType.Text:
          sb.Append(indent).AppendLine(node.Value);
          break;
        case XPType.CData:
          sb.Append(indent).Append("<![CDATA[").Append(node.Value).AppendLine("]]>");
          break;
        case XPType.ProcInst:
          sb.Append(indent).Append("<?").Append(node.Name.Local);
          sb.Append(' ').Append(node.Value).AppendLine("?>");
          break;
        case XPType.Comment:
          sb.Append(indent).Append("<!--").Append(node.Value).AppendLine("-->");
          break;
        default:
          throw new InvalidOperationException($"{node.Type}");
      }
      if (!siblings)
        break;
      index = node.NextSibling;
    }
  }

  private void AddNodeInline(
    int index, StringBuilder sb, int version, bool siblings = true)
  {
    while (index != -1)
    {
      ref var node = ref Latest(index, version);
      switch (node.Type)
      {
        case XPType.Namespace:
          sb.Append(' ').AppendXPName("xmlns", node.Name.Local);
          sb.Append('=').AppendQuoted(node.Value);
          break;
        case XPType.Attribute:
          sb.Append(' ').AppendXPName(node.Name).Append('=').AppendQuoted(node.Value);
          break;
        default:
          throw new InvalidOperationException($"{node.Type}");
      }
      if (!siblings)
        break;
      index = node.NextSibling;
    }
  }

  public void DebugDump()
  {
    Console.WriteLine($"V: {docVersion}");
    for (var i = 0; i <= docVersion; i++)
      Console.WriteLine($"ROOT {i} {roots[i]}");
    for (var i = 0; i < nodes.Length; i++)
    {
      ref var node = ref nodes[i];
      Console.WriteLine(
        $"NODE {i}={node.Index} {node.Type} " +
        $"[{node.Name.NsUri}]{node.Name.Prefix}:{node.Name.Local} = '{node.Value}' " +
        $"^{node.Parent} {node.PrevSibling}<>{node.NextSibling} " +
        $"a{node.FirstAttr}..{node.LastAttr} c{node.FirstContent}..{node.LastContent} " +
        $"v{node.DVersion} {node.VPrev}<>{node.VNext}"
      );
    }
  }
}

public static partial class Extensions
{
  public static StringBuilder AppendXPName(this StringBuilder sb, XPName name) =>
    sb.AppendXPName(name.Prefix, name.Local);

  public static StringBuilder AppendXPName(this StringBuilder sb, string prefix, string local)
  {
    if (prefix != "")
      sb.Append(prefix).Append(':');
    return sb.Append(local);
  }

  public static StringBuilder AppendQuoted(this StringBuilder sb, string value)
  {
    // TODO: maybe try to escape quotes?
    var quote = value.Contains('"') ? '\'' : '"';
    return sb.Append(quote).Append(value).Append(quote);
  }
}