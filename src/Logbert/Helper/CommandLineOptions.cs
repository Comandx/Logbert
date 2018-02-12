using System.Collections.Generic;
using CommandLine;

namespace Com.Couchcoding.Logbert.Helper
{
    class CommandLineOptions
    {
        [Value( 0, Max = 1, HelpText = "Logfile name" )]
        public IEnumerable<string> Files { get; set; }

        [Option( 'c', "custom-receiver", HelpText = "Add the custom receiver to the list of default receivers" )]
        public string CustomReceiver { get; set; }
    }
}
