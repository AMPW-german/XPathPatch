
using System;
using System.Text;

namespace XPP.Doc;

public partial class XPDocument
{
  public override string ToString()
  {
    var sb = new StringBuilder();
    AddNode(roots[^1], sb, "");
    return sb.ToString();
  }

  private void AddNode(int index, StringBuilder sb, string indent)
  {
    while (index != -1)
    {
      ref var node = ref Latest(index);
      switch (node.Type)
      {
        case XPType.Document:
          AddNode(node.FirstContent, sb, indent);
          break;
        case XPType.Element:
          sb.Append(indent).Append('<');
          sb.AppendXPName(node.Name);
          AddNodeInline(node.FirstAttr, sb);
          if (node.FirstContent == -1)
          {
            sb.AppendLine(" />");
            break;
          }
          sb.AppendLine(">");
          AddNode(node.FirstContent, sb, indent + "  ");
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
      index = node.NextSibling;
    }
  }

  private void AddNodeInline(int index, StringBuilder sb)
  {
    while (index != -1)
    {
      ref var node = ref Latest(index);
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
      index = node.NextSibling;
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