using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XPP.Tests;

[TestClass]
public partial class XPathTests
{

  private static string ListMsg<T>(List<T> expected, List<T> actual)
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

  private static IEnumerable<T> Concat<T>(
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

    public TreeComparer Child(string name, Action<TreeComparer> f)
    {
      Add(name);
      var pindent = indent;
      var pchild = child;
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
      return this;
    }

    public TreeComparer Compare(string name, bool match, string expected, string actual)
    {
      this.match &= match;
      Add(name);
      return match ? Add($"  = {expected}") : Add($"  < {expected}").Add($"  > {actual}");
    }

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