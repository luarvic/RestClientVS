﻿using System.Linq;
using RestClient.Parser;
using Xunit;

namespace RestClientTest
{
    public class VariableTest
    {
        [Theory]
        [InlineData("@name=value", "value")]
        [InlineData("@name = value", "value")]
        [InlineData("@name= value", "value")]
        [InlineData("@name =value", "value")]
        [InlineData("@name\t=\t value", "value")]
        public void VariableDeclarations(string line, string value)
        {
            var doc = Document.CreateFromLines(line);

            Variable first = doc.Variables?.FirstOrDefault();

            Assert.NotNull(first);
            Assert.Equal(0, first.Name.Start);
            Assert.EndsWith(value, first.Value.Text);
        }

        [Theory]
        [InlineData("var1", "1")]
        public void ExpandUrlVariables(string name, string value)
        {
            var variable = $"@{name}={value}";
            var request = "GET http://example.com?{{" + name + "}}";

            var doc = Document.CreateFromLines(variable, request);

            Request r = doc.Requests.FirstOrDefault();

            Assert.Equal("GET http://example.com?" + value, r.ToString());
        }

        [Fact]
        public void ExpandUrlVariablesRecursive()
        {
            var text = new[] { "@hostname=bing.com\r\n",
                       "@host={{hostname}}\r\n",
                       "GET https://{{host}}" };

            var doc = Document.CreateFromLines(text);

            Request r = doc.Requests.FirstOrDefault();

            Assert.Equal("GET https://bing.com", r.ToString());
        }

        [Fact]
        public void HeaderValueContainsVariable()
        {
            var text = new[] { "get http://ost.com\r\n",
                       "name:{{hostname}}\r\n" };

            var doc = Document.CreateFromLines(text);

            Assert.True(doc.Requests.First().Headers.First().Value.References.Any());
        }
    }
}
