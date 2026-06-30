using System.Collections.Generic;

namespace VideoToAnimationTool.Core.ExternalTools
{
    public sealed class ExternalToolRunOptions
    {
        public ExternalToolRunOptions()
        {
            Arguments = new string[0];
            EnvironmentPathEntries = new string[0];
        }

        public string FileName { get; set; }
        public string[] Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string[] EnvironmentPathEntries { get; set; }
        public IDictionary<string, string> EnvironmentVariables { get; set; }
    }
}
