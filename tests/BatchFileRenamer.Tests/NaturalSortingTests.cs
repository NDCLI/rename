using System;
using System.Collections.Generic;
using System.Linq;
using BatchFileRenamer.Helpers;
using BatchFileRenamer.Models;
using BatchFileRenamer.Services;
using Xunit;

namespace BatchFileRenamer.Tests
{
    public class NaturalSortingTests
    {
        [Fact]
        public void NaturalStringComparer_SortsNumbersNaturally()
        {
            var rawList = new List<string> { "file10.txt", "file2.txt", "file1.txt", "file20.txt", "file3.txt" };
            var sorted = rawList.OrderBy(x => x, NaturalStringComparer.Default).ToList();

            Assert.Equal(new[] { "file1.txt", "file2.txt", "file3.txt", "file10.txt", "file20.txt" }, sorted);
        }

        [Fact]
        public void NaturalStringComparer_SortsComplexNamesWithLeadingZeros()
        {
            var rawList = new List<string> { "doc_02.pdf", "doc_1.pdf", "doc_10.pdf", "doc_003.pdf" };
            var sorted = rawList.OrderBy(x => x, NaturalStringComparer.Default).ToList();

            Assert.Equal("doc_1.pdf", sorted[0]);
            Assert.Equal("doc_02.pdf", sorted[1]);
            Assert.Equal("doc_003.pdf", sorted[2]);
            Assert.Equal("doc_10.pdf", sorted[3]);
        }
    }
}
