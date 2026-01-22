
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using XPP.Doc;
using XPP.Path;

namespace XPP.Patch;

public class PatchDomain : IXPathUserContext
{
  protected virtual string RootElName => "Root";
  protected virtual string ModElName => "Mod";
  protected virtual string ModIdAttr => "Id";
  protected virtual string PatchElName => "Patch";
  protected virtual string FilePathAttr => "Path";
  protected virtual XmlSerializer PatchDeserializer =>
    field ??= new(typeof(PatchFile), new XmlRootAttribute(PatchElName));
  public virtual PatchOpDeserializer OpDeserializer => field ??= new();

  private const string PATCH_VAR = "patch";
  private readonly Dictionary<string, ExecResult> userVars = [];

  public readonly XPDocument Doc;
  protected readonly List<PatchMod> mods = [];

  public XPNodeRef Root => Doc.LatestRoot.FirstContent;
  public virtual IEnumerable<PatchMod> Mods => mods;

  public PatchExecutor Executor() => new(this);

  public IEnumerable<XPNodeRef> Patches
  {
    get
    {
      foreach (var mod in Mods)
      {
        var child = mod.Node.LatestVersion.FirstContent;
        while (child.Valid)
        {
          if (child.Name == new XPName("", "", PatchElName))
            yield return child;
          child = child.NextSibling;
        }
      }
    }
  }

  public PatchDomain()
  {
    Doc = XPDocument.New();
    var rootEl = Doc.LatestRoot.AddElement(RootElName);
    rootEl.SetUpdateIgnore(XPUpdateType.Delete);
  }

  public virtual PatchMod AddMod(string id)
  {
    var node = Root.AddElement(ModElName);
    node.SetUpdateIgnore(XPUpdateType.Delete);
    var idAttr = node.AddAttribute(ModIdAttr, id);
    idAttr.SetUpdateIgnore(XPUpdateType.Delete | XPUpdateType.Value);
    var mod = new PatchMod(this, id, node);
    mods.Add(mod);
    return mod;
  }

  public ExecResult ExecXPath(string path, XPNodeRef context, XPNodeRef patch)
  {
    userVars[PATCH_VAR] = new(new ExecPathOpNodeList([patch]));
    return XPath.Exec(path, context, this);
  }

  public void SetVariable(string name, List<ExecValue> values)
  {
    if (values.Count == 0)
    {
      userVars[name] = new(new ExecPathOpNodeList([]));
      return;
    }
    var first = values[0];
    userVars[name] = first.Type switch
    {
      XPValueType.Bool => new(first.Bool),
      XPValueType.Number => new(first.Number),
      XPValueType.String => new(first.String),
      XPValueType.NodeSet => new(new ExecPathOpNodeList([.. values.Select(v => v.Node)])),
      _ => throw new InvalidOperationException($"{first.Type}"),
    };
  }

  public ExecResult GetVariable(string name, ExecPathCtx context)
  {
    if (!userVars.TryGetValue(name, out var val))
      throw new InvalidOperationException($"unknown var {name}");

    if (val.Type is XPValueType.NodeSet)
      val.NodeSet.Init(context.Nav);
    return val;
  }

  public ExecResult CallFunc(string name, ExecPathCtx context, ExecResult[] args)
  {
    throw new NotImplementedException();
  }

  public readonly struct PatchMod(PatchDomain Domain, string Id, XPNodeRef Node)
  {
    public readonly PatchDomain Domain = Domain;
    public readonly string Id = Id;
    public readonly XPNodeRef Node = Node;

    public XPNodeRef ImportFile(string fileName) => Import(fileName,
      XmlReader.Create(fileName, new() { IgnoreWhitespace = true }));

    public XPNodeRef ImportXml(string fileName, string xml) => Import(fileName,
      XmlReader.Create(new StringReader(xml), new() { IgnoreWhitespace = true }));

    public XPNodeRef Import(string fileName, XmlReader reader)
    {
      var node = Node.Import(reader);
      node.SetUpdateIgnore(XPUpdateType.Delete);
      var pathAttr = node.SetAttribute(Domain.FilePathAttr, NormalizePath(fileName));
      pathAttr.SetUpdateIgnore(XPUpdateType.Delete | XPUpdateType.Value);
      return node;
    }

    public IEnumerable<XPNodeRef> Files(string elName)
    {
      if (!Node.ResolveName(elName, out var name))
        yield break;
      var child = Node.LatestVersion.FirstContent;
      while (child.Valid)
      {
        if (child.Type is XPType.Element && child.Name == name)
          yield return child;
        child = child.NextSibling;
      }
    }

    private static readonly bool caseSensitive =
      !(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());
    private static string NormalizePath(string path)
    {
      path = path.Replace('\\', '/');
      if (!caseSensitive)
        path = path.ToLowerInvariant();
      return path;
    }
  }
}