
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XPP.Doc;

namespace XPP.Tests;

public abstract class BaseTest
{
  public record class PathStep(string Name, XPType Type, int Index)
  {
    public static implicit operator PathStep(string path)
    {
      string desc, idx;
      var split = path.IndexOf('#');
      if (split == -1)
        (desc, idx) = (path, "0");
      else
        (desc, idx) = (path[..split], path[(split + 1)..]);

      var index = int.Parse(idx);
      return desc[0] switch
      {
        '\'' => new("", XPType.Text, index),
        '[' => new("", XPType.CData, index),
        '?' => new(desc[1..], XPType.ProcInst, index),
        '!' => new("", XPType.Comment, index),
        '@' => new(desc[1..], XPType.Attribute, index),
        ':' => new(desc[1..], XPType.Namespace, index),
        _ => new(desc, XPType.Element, index),
      };
    }

    public override string ToString()
    {
      var prefix = Type switch
      {
        XPType.Text => "'",
        XPType.CData => "[",
        XPType.ProcInst => "?",
        XPType.Comment => "!",
        XPType.Attribute => "@",
        XPType.Namespace => ":",
        _ => "",
      };
      return $"{prefix}{Name}#{Index}";
    }

    public XPNodeRef Get(XPNodeRef parent)
    {
      var node = Type.IsAttribute ? parent.FirstAttr : parent.FirstContent;
      var count = 0;
      while (node.Valid)
      {
        if (node.Type == Type && node.Name.Local == Name)
        {
          if (count == Index)
            return node;
          count++;
        }
        node = node.NextSibling;
      }
      return node;
    }
  }

  public class Path : List<PathStep>, IEquatable<Path>
  {
    public bool Equals(Path other) => Enumerable.SequenceEqual(this, other);
    public override bool Equals(object obj) => obj is Path other && Equals(other);
    public override string ToString() => string.Join('/', this);
    public override int GetHashCode()
    {
      var hash = new HashCode();
      foreach (var step in this)
        hash.Add(step);
      return hash.ToHashCode();
    }

    public XPNodeRef Get(XPNodeRef root)
    {
      var node = root;
      foreach (var step in this)
        node = step.Get(node);
      return node;
    }

    public static implicit operator Path(string path) =>
      [.. path.Split('/').Select(p => (PathStep)p)];

    public static Path FromNode(XPNodeRef node)
    {
      var path = new Path();
      while (node.Valid && node.Type is not XPType.Document)
      {
        var index = 0;
        var prev = node.PrevSibling;
        while (prev.Valid)
        {
          if (prev.Type == node.Type && prev.Name == node.Name)
            index++;
          prev = prev.PrevSibling;
        }
        path.Add(new(node.Name.Local, node.Type, index));
        node = node.Parent;
      }
      path.Reverse();
      return path;
    }
  }

  public static void XPNodeEqual(XPNodeRef expected, XPNodeRef actual)
  {
    var t = new TreeComparer("");
    XPNodeEqual(t, expected, actual);
    t.Assert();
  }

  private static void XPNodeEqual(TreeComparer t, XPNodeRef expected, XPNodeRef actual)
  {
    if (!expected.Valid && !actual.Valid)
      return;
    t.Compare("Type", expected.Type == actual.Type, expected.Type, actual.Type);
    if (expected.Type.HasName || actual.Type.HasName)
      t.Compare("Name", expected.Name == actual.Name,
        expected.Name.Local, actual.Name.Local);
    if (expected.Type.HasValue || actual.Type.HasValue)
      t.Compare("Value", expected.Value == actual.Value,
        $"'{expected.Value}'", $"'{actual.Value}'");

    var eattr = expected.FirstAttr;
    var aattr = actual.FirstAttr;
    var index = 0;
    while (eattr.Valid || aattr.Valid)
    {
      t.Child($"Attr {index}", t => XPNodeEqual(t, eattr, aattr));
      index++;
      eattr = eattr.NextSibling;
      aattr = aattr.NextSibling;
    }

    var econtent = expected.FirstContent;
    var acontent = actual.FirstContent;
    index = 0;
    while (econtent.Valid || acontent.Valid)
    {
      t.Child($"Content {index}", t => XPNodeEqual(t, econtent, acontent), true);
      index++;
      econtent = econtent.NextSibling;
      acontent = acontent.NextSibling;
    }
  }

  public static string ListMsg<T>(List<T> expected, List<T> actual)
  {
    var sb = new StringBuilder();
    sb.AppendLine();
    for (var i = 0; i < expected.Count && i < actual.Count; i++)
    {
      if (EqualityComparer<T>.Default.Equals(expected[i], actual[i]))
        sb.AppendLine($"{i} = {expected[i]}");
      else
      {
        sb.AppendLine($"{i} < {expected[i]}");
        sb.AppendLine($"{i} > {actual[i]}");
      }
    }
    for (var i = actual.Count; i < expected.Count; i++)
      sb.AppendLine($"{i} < {expected[i]}");
    for (var i = expected.Count; i < actual.Count; i++)
      sb.AppendLine($"{i} > {actual[i]}");
    return sb.ToString();
  }

  public static IEnumerable<T> Concat<T>(
    IEnumerable<T> first, params IEnumerable<T>[] rest)
  {
    var res = first;
    foreach (var r in rest)
      res = res.Concat(r);
    return res;
  }

  public class TreeComparer(string title)
  {
    public static TreeComparer New(string title) => new(title);

    private readonly StringBuilder sb = new(title + "\n");
    private string indent = "";
    private bool child = false;
    private bool match = true;

    public TreeComparer Add(string info)
    {
      sb.Append(indent).AppendLine(info);
      return this;
    }

    public TreeComparer Child(
      string name, Action<TreeComparer> f, bool omitMatch = false)
    {
      var startLen = sb.Length;
      Add(name);
      var pindent = indent;
      var pchild = child;
      var pmatch = match;
      match = true;
      indent += "  ";
      child = true;
      try
      {
        f(this);
      }
      catch (AssertFailedException)
      {
        match = false;
      }
      indent = pindent;
      child = pchild;
      if (omitMatch && match)
        sb.Length = startLen;
      match = match && pmatch;
      return this;
    }

    public TreeComparer Compare(string name, bool match, string expected, string actual)
    {
      this.match &= match;
      return match
        ? Add($"{name} = {expected}")
        : Add(name).Add($"  < {expected}").Add($"  > {actual}");
    }

    public TreeComparer Compare<T>(string name, bool match, T expected, T actual) =>
      Compare(name, match, $"{expected}", $"{actual}");

    public TreeComparer CmpThrow(string name, bool match, string expected, string actual)
    {
      Compare(name, match, expected, actual);
      if (!match) throw new AssertFailedException();
      return this;
    }

    public TreeComparer CmpThrow<T>(string name, bool match, T expected, T actual) =>
      CmpThrow(name, match, $"{expected}", $"{actual}");

    public TreeComparer Fail()
    {
      match = false;
      return this;
    }

    public void Assert()
    {
      if (!match)
        throw new InvalidOperationException($"\n{sb}");
    }
  }
}