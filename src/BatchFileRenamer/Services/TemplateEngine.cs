using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BatchFileRenamer.Models;

namespace BatchFileRenamer.Services
{
    public interface ITemplateEngine
    {
        string GenerateFileName(RenameTemplateOptions options, int selectedIndex, RenameItem? item = null);
        bool ValidateTemplate(string template, out string errorMessage);
    }

    public class TemplateEngine : ITemplateEngine
    {
        private static readonly Regex TokenRegex = new(@"\{(?<token>[a-zA-Z0-9_]+)(?::(?<format>[^}]+))?\}", RegexOptions.Compiled);

        public string GenerateFileName(RenameTemplateOptions options, int selectedIndex, RenameItem? item = null)
        {
            if (string.IsNullOrWhiteSpace(options.Template))
            {
                return options.BaseName ?? string.Empty;
            }

            var culture = GetCulture(options.CultureLanguage);
            var date = options.StartDate.AddDays(selectedIndex * Math.Max(1, options.DayStep));
            var number = options.StartNumber + (selectedIndex * Math.Max(1, options.NumberStep));

            string result = TokenRegex.Replace(options.Template, match =>
            {
                string token = match.Groups["token"].Value.ToLowerInvariant();
                string format = match.Groups["format"].Success ? match.Groups["format"].Value : string.Empty;

                switch (token)
                {
                    case "name":
                        return options.BaseName ?? string.Empty;

                    case "date":
                        if (string.IsNullOrEmpty(format))
                        {
                            return date.ToString("yyyy-MM-dd", culture);
                        }
                        try
                        {
                            return date.ToString(format, culture);
                        }
                        catch
                        {
                            return date.ToString("yyyy-MM-dd", culture);
                        }

                    case "n":
                    case "seq":
                    case "index":
                    case "num":
                        if (string.IsNullOrEmpty(format))
                        {
                            return number.ToString(culture);
                        }
                        try
                        {
                            return number.ToString(format, culture);
                        }
                        catch
                        {
                            return number.ToString(culture);
                        }

                    case "orig":
                    case "original":
                        return item?.OriginalFileNameWithoutExtension ?? string.Empty;

                    case "ext":
                        return item?.Extension.TrimStart('.') ?? string.Empty;

                    default:
                        // Keep unknown token as is
                        return match.Value;
                }
            });

            return result;
        }

        public bool ValidateTemplate(string template, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(template))
            {
                errorMessage = "Mẫu tên không được để trống.";
                return false;
            }

            // Check balanced braces
            int braceCount = 0;
            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] == '{')
                {
                    braceCount++;
                    if (braceCount > 1)
                    {
                        errorMessage = "Mẫu tên chứa dấu ngoặc lồng nhau không hợp lệ.";
                        return false;
                    }
                }
                else if (template[i] == '}')
                {
                    braceCount--;
                    if (braceCount < 0)
                    {
                        errorMessage = "Dấu ngoặc đóng '}' không có dấu mở tương ứng.";
                        return false;
                    }
                }
            }

            if (braceCount != 0)
            {
                errorMessage = "Dấu ngoặc mở '{' chưa được đóng.";
                return false;
            }

            return true;
        }

        private static CultureInfo GetCulture(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                return CultureInfo.InvariantCulture;
            }

            try
            {
                return CultureInfo.GetCultureInfo(lang);
            }
            catch
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }
}
