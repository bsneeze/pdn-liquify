using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace pyrochild.effects.liquify.tests
{
    /// <summary>
    /// The plugin is built against Paint.NET's assemblies without copying them, because inside
    /// Paint.NET they are already loaded. The tests run outside it, so this points the runtime at the
    /// Paint.NET folder for both the managed assemblies and the native DLLs they depend on.
    ///
    /// This only takes effect once some code in the test assembly runs. Test discovery happens before
    /// that, by reflection, so test classes must not have fields of Paint.NET types (locals, parameters
    /// and properties are fine). If one does, discovery fails to load the type and reports no tests.
    /// </summary>
    internal static class PaintDotNetAssemblies
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            string dir = typeof(PaintDotNetAssemblies).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .First(a => a.Key == "PdnDir")
                .Value;

            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                string path = Path.Combine(dir, name.Name + ".dll");
                return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
            };

            Environment.SetEnvironmentVariable("PATH", dir + ";" + Environment.GetEnvironmentVariable("PATH"));
        }
    }
}
