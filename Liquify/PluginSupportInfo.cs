using System;
using System.Reflection;
using PaintDotNet;

namespace pyrochild.effects
{
    class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author
        {
            get { return ((AssemblyCompanyAttribute)this.GetType().Assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false)[0]).Company; }
        }

        public string Copyright
        {
            get { return ((AssemblyCopyrightAttribute)this.GetType().Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0]).Copyright; }
        }

        public string DisplayName
        {
            get { return ((AssemblyProductAttribute)this.GetType().Assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false)[0]).Product; }
        }

        public Version Version
        {
            get { return this.GetType().Assembly.GetName().Version; }
        }

        public Uri WebsiteUri
        {
            get { return new Uri("http://forums.getpaint.net/index.php?showtopic=7291"); }
        }
    }
}