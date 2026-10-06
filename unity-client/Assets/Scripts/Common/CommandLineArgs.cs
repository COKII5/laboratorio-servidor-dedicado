using System;

namespace Lab.Common
{
    /// <summary>
    /// Lee argumentos de la forma "-nombre valor" al lanzar una compilación. Permite abrir
    /// dos copias del mismo ejecutable como jugadores distintos sin recompilar.
    /// </summary>
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
