using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public class Tools
    {
        // Méthode simple qui permet de vérifier si l'argument passé est une URL ou non.
        public static bool IsAnURL(string args)
        {
            return args.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        }
    }
}
