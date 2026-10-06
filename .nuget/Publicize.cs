using Mono.Cecil;
using System;

class Publicize
{
  static void Main(string[] args)
  {
    var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(args[0]));
    var reader = new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Deferred };
    var asm = AssemblyDefinition.ReadAssembly(args[0], reader);
    foreach (var type in asm.MainModule.Types)
      MakePublic(type);
    asm.Write(args[1]);
    Console.WriteLine("Wrote " + args[1]);
  }

  static void MakePublic(TypeDefinition type)
  {
    if (type.IsNested)
      type.IsNestedPublic = true;
    else
      type.IsPublic = true;

    foreach (var method in type.Methods)
    {
      if (method.IsCompilerControlled)
        continue;
      method.IsPublic = true;
    }
    foreach (var field in type.Fields)
      field.IsPublic = true;
    foreach (var nested in type.NestedTypes)
      MakePublic(nested);
  }
}
