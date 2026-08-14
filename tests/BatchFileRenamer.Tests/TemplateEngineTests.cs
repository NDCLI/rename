using System;
using System.Globalization;
using BatchFileRenamer.Models;
using BatchFileRenamer.Services;
using Xunit;

namespace BatchFileRenamer.Tests
{
    public class TemplateEngineTests
    {
        private readonly TemplateEngine _engine = new();

        [Fact]
        public void GenerateFileName_ExampleFromSpec_ProducesCorrectEnglishMonthAndFormat()
        {
            // Mẫu: {name} ({date:MMM d, yyyy})
            // Tên chính: Re-ID Hoa
            // Ngày bắt đầu: 14/08/2026
            // Bước tăng: 1 ngày
            // Kết quả: Re-ID Hoa (Aug 14, 2026), Re-ID Hoa (Aug 15, 2026)...
            var options = new RenameTemplateOptions
            {
                Template = "{name} ({date:MMM d, yyyy})",
                BaseName = "Re-ID Hoa",
                StartDate = new DateTime(2026, 8, 14),
                DayStep = 1,
                CultureLanguage = "en-US"
            };

            string file0 = _engine.GenerateFileName(options, 0);
            string file1 = _engine.GenerateFileName(options, 1);
            string file2 = _engine.GenerateFileName(options, 2);

            Assert.Equal("Re-ID Hoa (Aug 14, 2026)", file0);
            Assert.Equal("Re-ID Hoa (Aug 15, 2026)", file1);
            Assert.Equal("Re-ID Hoa (Aug 16, 2026)", file2);
        }

        [Fact]
        public void GenerateFileName_TransitionAcrossMonthEnd_CalculatesCorrectly()
        {
            // 31/08/2026 + 1 day -> 01/09/2026
            var options = new RenameTemplateOptions
            {
                Template = "{name}_{date:yyyy-MM-dd}",
                BaseName = "Item",
                StartDate = new DateTime(2026, 8, 31),
                DayStep = 1,
                CultureLanguage = "en-US"
            };

            string file0 = _engine.GenerateFileName(options, 0);
            string file1 = _engine.GenerateFileName(options, 1);

            Assert.Equal("Item_2026-08-31", file0);
            Assert.Equal("Item_2026-09-01", file1);
        }

        [Fact]
        public void GenerateFileName_TransitionAcrossYearEnd_CalculatesCorrectly()
        {
            // 31/12/2026 + 1 day -> 01/01/2027
            var options = new RenameTemplateOptions
            {
                Template = "{name}_{date:MMM d, yyyy}",
                BaseName = "YearTransition",
                StartDate = new DateTime(2026, 12, 31),
                DayStep = 1,
                CultureLanguage = "en-US"
            };

            string file0 = _engine.GenerateFileName(options, 0);
            string file1 = _engine.GenerateFileName(options, 1);

            Assert.Equal("YearTransition_Dec 31, 2026", file0);
            Assert.Equal("YearTransition_Jan 1, 2027", file1);
        }

        [Fact]
        public void GenerateFileName_LeapYear_HandlesFeb29Correctly()
        {
            // 2028 is a leap year (28/02/2028 -> 29/02/2028 -> 01/03/2028)
            var options = new RenameTemplateOptions
            {
                Template = "{date:dd-MM-yyyy}",
                BaseName = "Leap",
                StartDate = new DateTime(2028, 2, 28),
                DayStep = 1,
                CultureLanguage = "en-US"
            };

            string day28 = _engine.GenerateFileName(options, 0);
            string day29 = _engine.GenerateFileName(options, 1);
            string dayMar1 = _engine.GenerateFileName(options, 2);

            Assert.Equal("28-02-2028", day28);
            Assert.Equal("29-02-2028", day29);
            Assert.Equal("01-03-2028", dayMar1);
        }

        [Fact]
        public void GenerateFileName_DayStepGreaterThanOne_CalculatesProperIntervals()
        {
            // Step = 3 days
            var options = new RenameTemplateOptions
            {
                Template = "{name} ({date:MMM d, yyyy})",
                BaseName = "Event",
                StartDate = new DateTime(2026, 8, 14),
                DayStep = 3,
                CultureLanguage = "en-US"
            };

            string file0 = _engine.GenerateFileName(options, 0);
            string file1 = _engine.GenerateFileName(options, 1);
            string file2 = _engine.GenerateFileName(options, 2);

            Assert.Equal("Event (Aug 14, 2026)", file0);
            Assert.Equal("Event (Aug 17, 2026)", file1);
            Assert.Equal("Event (Aug 20, 2026)", file2);
        }

        [Fact]
        public void GenerateFileName_NumberFormatting_SupportsZeroPaddingAndCustomSteps()
        {
            var options = new RenameTemplateOptions
            {
                Template = "{name}_{n:000}",
                BaseName = "Document",
                StartNumber = 5,
                NumberStep = 5
            };

            string file0 = _engine.GenerateFileName(options, 0);
            string file1 = _engine.GenerateFileName(options, 1);
            string file2 = _engine.GenerateFileName(options, 2);

            Assert.Equal("Document_005", file0);
            Assert.Equal("Document_010", file1);
            Assert.Equal("Document_015", file2);
        }

        [Fact]
        public void GenerateFileName_VietnameseCharactersAndComplexTokens_PreservesEncoding()
        {
            var options = new RenameTemplateOptions
            {
                Template = "Hồ Sơ [{name}] - Số {n:D2} - Ngày {date:dd/MM/yyyy}",
                BaseName = "Nguyễn Văn Ánh - Dự Án Đổi Mới",
                StartDate = new DateTime(2026, 8, 14),
                StartNumber = 1
            };

            string file0 = _engine.GenerateFileName(options, 0);
            Assert.Equal("Hồ Sơ [Nguyễn Văn Ánh - Dự Án Đổi Mới] - Số 01 - Ngày 14/08/2026", file0);
        }

        [Fact]
        public void GenerateFileName_FlexibleStructures_SupportsOrigPrefixSuffixAndParentFolder()
        {
            var item = new RenameItem
            {
                OriginalFileNameWithoutExtension = "IMG_0088",
                OriginalDirectory = @"D:\Photos\Travel_2026",
                Extension = ".jpg"
            };

            // Test 1: Prefix + Original
            var optPrefix = new RenameTemplateOptions { Template = "{name}_{orig}", BaseName = "Vietnam" };
            Assert.Equal("Vietnam_IMG_0088", _engine.GenerateFileName(optPrefix, 0, item));

            // Test 2: Original + Suffix
            var optSuffix = new RenameTemplateOptions { Template = "{orig}_{name}", BaseName = "Edited" };
            Assert.Equal("IMG_0088_Edited", _engine.GenerateFileName(optSuffix, 0, item));

            // Test 3: Parent Folder + Number
            var optParent = new RenameTemplateOptions { Template = "{parent}_{n:000}", StartNumber = 1 };
            Assert.Equal("Travel_2026_001", _engine.GenerateFileName(optParent, 0, item));

            // Test 4: Pure Numbering
            var optNumOnly = new RenameTemplateOptions { Template = "{n:0000}", StartNumber = 42 };
            Assert.Equal("0042", _engine.GenerateFileName(optNumOnly, 0, item));

            // Test 5: Case Modifiers
            var optCase = new RenameTemplateOptions { Template = "{name:upper}_{orig:lower}", BaseName = "hdr" };
            Assert.Equal("HDR_img_0088", _engine.GenerateFileName(optCase, 0, item));
        }

        [Theory]
        [InlineData("{name}_{date:yyyyMMdd}", true)]
        [InlineData("{name}_{n:000}", true)]
        [InlineData("Static Text Only", true)]
        [InlineData("{name", false)] // Unclosed brace
        [InlineData("name}", false)] // Unopened brace
        [InlineData("{name_{date}}", false)] // Nested brace
        [InlineData("", false)] // Empty template
        public void ValidateTemplate_DetectsSyntaxIssues(string template, bool expectedValid)
        {
            bool isValid = _engine.ValidateTemplate(template, out string error);
            Assert.Equal(expectedValid, isValid);
            if (!expectedValid)
            {
                Assert.NotEmpty(error);
            }
        }
    }
}
