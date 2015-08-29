﻿using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using IllyumL2T.Core.FieldsSplit.Parse;
using IllyumL2T.Core.FieldsSplit.UnitTests.Classes_for_Testing;
using System.Text;

namespace IllyumL2T.Core.FieldsSplit.UnitTests
{
  [TestClass]
  public class FixedWidthValuesFileParserTests
  {
    [TestMethod]
    public void ParseLineTestOneLine()
    {
      // Arrange
      TextReader reader = new StringReader("1950026ABC");
      var fileParser = new FixedWidthValuesFileParser<Record>();

      // Act
      var parseResults = fileParser.Read(reader, includeHeaders: false);
      var parsedObjects = new List<Record>(parseResults.Select(parseResult => parseResult.Instance));

      // Assert
      Assert.IsNotNull(parsedObjects);
      Assert.AreEqual<int>(1, parsedObjects.Count());
      Assert.IsNotNull(parsedObjects[0]);
      Assert.AreEqual<ushort>(19, parsedObjects[0].Type);
      Assert.AreEqual<byte>(5, parsedObjects[0].Category);
      Assert.AreEqual<uint>(26, parsedObjects[0].ID);
      Assert.AreEqual<string>("ABC", parsedObjects[0].Label);
    }

    [TestMethod]
    public void ParseLineTestTwoLines()
    {
      // Arrange
      var lines = new StringBuilder();
      lines.AppendLine("0015432 XY");
      lines.AppendLine("1950026ABC");
      TextReader reader = new StringReader(lines.ToString());
      var fileParser = new FixedWidthValuesFileParser<Record>();

      // Act
      var parseResults = fileParser.Read(reader, includeHeaders: false);
      var parsedObjects = new List<Record>(parseResults.Select(parseResult => parseResult.Instance));

      // Assert
      Assert.IsNotNull(parsedObjects);
      Assert.AreEqual<int>(2, parsedObjects.Count());

      Assert.IsNotNull(parsedObjects[0]);
      Assert.AreEqual<ushort>(0, parsedObjects[0].Type);
      Assert.AreEqual<byte>(1, parsedObjects[0].Category);
      Assert.AreEqual<uint>(5432, parsedObjects[0].ID);
      Assert.AreEqual<string>(" XY", parsedObjects[0].Label);

      Assert.IsNotNull(parsedObjects[1]);
      Assert.AreEqual<ushort>(19, parsedObjects[1].Type);
      Assert.AreEqual<byte>(5, parsedObjects[1].Category);
      Assert.AreEqual<uint>(26, parsedObjects[1].ID);
      Assert.AreEqual<string>("ABC", parsedObjects[1].Label);
    }
  }
}