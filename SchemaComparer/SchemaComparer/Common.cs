using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchemaComparer
{
    public static class Common
    {
        public static string GetLastDelimitedString(this string input, string delimiter)
        {
            string[] arr = input.Split(delimiter, StringSplitOptions.None);
            return arr[^1];
        }
    }
}
