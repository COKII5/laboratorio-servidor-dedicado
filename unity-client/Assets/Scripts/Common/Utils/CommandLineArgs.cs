using System;

namespace Lab.Common
{
    public static class CommandLineArgs
    {
        public static bool TryGet(string name, out string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i + 1];
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
