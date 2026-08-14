using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BatchFileRenamer.Helpers
{
    public class NaturalStringComparer : IComparer<string>, IComparer
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        private static bool _usePInvoke = true;

        public static NaturalStringComparer Default { get; } = new NaturalStringComparer();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            if (_usePInvoke)
            {
                try
                {
                    return StrCmpLogicalW(x, y);
                }
                catch
                {
                    _usePInvoke = false;
                }
            }

            return CompareManagedFallback(x, y);
        }

        public int Compare(object? x, object? y)
        {
            return Compare(x as string, y as string);
        }

        private static int CompareManagedFallback(string x, string y)
        {
            // Split into numbers and non-numbers chunks
            string[] xParts = Regex.Split(x.Replace(" ", " "), "([0-9]+)");
            string[] yParts = Regex.Split(y.Replace(" ", " "), "([0-9]+)");

            for (int i = 0; i < xParts.Length && i < yParts.Length; i++)
            {
                if (xParts[i] != yParts[i])
                {
                    if (long.TryParse(xParts[i], out long xNum) && long.TryParse(yParts[i], out long yNum))
                    {
                        return xNum.CompareTo(yNum);
                    }
                    return string.Compare(xParts[i], yParts[i], StringComparison.OrdinalIgnoreCase);
                }
            }

            return xParts.Length.CompareTo(yParts.Length);
        }
    }
}
