
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using XPP.Doc;

namespace XPP.Patch;

public class PatchDomain
{
  protected virtual string RootElName => "Root";
  protected virtual string ModElName => "Mod";
  protected virtual string ModIdAttr => "Id";
  protected virtual string PatchElName => "Patch";
  protected virtual string FilePathAttr => "Path";
  protected virtual XmlSerializer PatchDeserializer =>
    field ??= new(typeof(PatchFile), new XmlRootAttribute(PatchElName));
  public virtual PatchOpDeserializer OpDeserializer => field ??= new();

  public readonly XPDocument Doc;
  protected readonly List<PatchMod> mods = [];

  public XPNodeRef Root => Doc.LatestRoot.FirstContent;
  public virtual IEnumerable<PatchMod> Mods => mods;

  public IEnumerable<XPNodeRef> Patches
  {
    get
    {
      foreach (var mod in Mods)
      {
        var child = mod.Node.FirstContent;
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
    Doc.LatestRoot.AddElement(RootElName);
  }

  public virtual PatchMod AddMod(string id)
  {
    var node = Root.AddElement(ModElName);
    node.AddAttribute(ModIdAttr, id);
    var mod = new PatchMod(this, id, node);
    mods.Add(mod);
    return mod;
  }

  public readonly struct PatchMod(PatchDomain Domain, string Id, XPNodeRef Node)
  {
    public readonly PatchDomain Domain = Domain;
    public readonly string Id = Id;
    public readonly XPNodeRef Node = Node;

    public XPNodeRef ImportFile(string fileName)
    {
      var node = Node.Import(
        XmlReader.Create(fileName, new() { IgnoreWhitespace = true }));
      node.SetAttribute(Domain.FilePathAttr, fileName.Replace('\\', '/'));
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
  }
}