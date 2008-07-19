/*
   Copyright 2008 Louis DeJardin - http://whereslou.com

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Spark.Parser;
using Spark.Parser.Markup;
using NUnit.Framework;
using Rhino.Mocks;
using Spark.FileSystem;

namespace Spark.Tests
{
    [TestFixture]
    public class ViewLoaderTester
    {
        private MockRepository mocks;
        private ViewLoader loader;

        private IViewFolder viewSourceLoader;

        private Dictionary<char, IList<Node>> nodesTable;

        private long _lastModified;

        [SetUp]
        public void Init()
        {
            mocks = new MockRepository();

            nodesTable = new Dictionary<char, IList<Node>>();

            viewSourceLoader = mocks.CreateMock<IViewFolder>();
            SetupResult.For(viewSourceLoader.ListViews("home")).Return(new[] { "file.spark", "other.spark", "_comment.spark" });
            SetupResult.For(viewSourceLoader.ListViews("Home")).Return(new[] { "file.spark", "other.spark", "_comment.spark" });
            SetupResult.For(viewSourceLoader.ListViews("Account")).Return(new[] { "index.spark" });
            SetupResult.For(viewSourceLoader.ListViews("Shared")).Return(new[] { "layout.spark", "_header.spark", "default.spark", "_footer.spark" });


            loader = new ViewLoader { ViewFolder = viewSourceLoader };
            loader.Parser = delegate(Position input)
                                {
                                    return new ParseResult<IList<Node>>(input.Advance(1), nodesTable[input.Peek()]);
                                };
        }

        long GetLastModified()
        {
            return _lastModified;
        }

        void ExpectGetSource(string path, IList<Node> nodes)
        {
            var source = mocks.CreateMock<IViewFile>();
            int key = '0' + nodesTable.Count;
            nodesTable.Add((char)key, nodes);

            Stream stream = new MemoryStream(new[] { (byte)key });
            SetupResult.For(source.OpenViewStream()).Return(stream);
            SetupResult.For(source.LastModified).Do(new Func<long>(GetLastModified));

            SetupResult.For(viewSourceLoader.GetViewSource(path)).Return(source);
        }

        static Node ParseElement(string content)
        {
            var grammar = new MarkupGrammar();
            var result = grammar.Element(new Position(new SourceContext(content)));
            Assert.IsNotNull(result);
            return result.Value;
        }

        [Test]
        public void LoadSimpleFile()
        {
            ExpectGetSource("home\\simple.spark", new Node[0]);

            mocks.ReplayAll();
            loader.Load("home\\simple.spark");
            mocks.VerifyAll();
        }

        [Test]
        public void LoadUsedFile()
        {
            var useFile = ParseElement("<use file='mypartial'/>");

            ExpectGetSource("Home\\usefile.spark", new[] { useFile });
            Expect.Call(viewSourceLoader.HasView("Home\\mypartial.spark")).Return(true);
            ExpectGetSource("Home\\mypartial.spark", new Node[0]);

            mocks.ReplayAll();
            loader.Load("Home\\usefile.spark");
            mocks.VerifyAll();
        }


        [Test]
        public void LoadSharedFile()
        {
            var useFile = ParseElement("<use file='mypartial'/>");

            ExpectGetSource("Home\\usefile.spark", new[] { useFile });
            Expect.Call(viewSourceLoader.HasView("Home\\mypartial.spark")).Return(false);
            Expect.Call(viewSourceLoader.HasView("Shared\\mypartial.spark")).Return(true);
            ExpectGetSource("Shared\\mypartial.spark", new Node[0]);

            mocks.ReplayAll();
            loader.Load("Home\\usefile.spark");
            mocks.VerifyAll();
        }

        [Test]
        public void FindPartialFiles()
        {
            mocks.ReplayAll();
            var partials3 = loader.FindPartialFiles("Home\\other.spark");
            var partials2 = loader.FindPartialFiles("Account\\index.spark");
            mocks.VerifyAll();

            Assert.AreEqual(3, partials3.Count);
            Assert.That(partials3.Contains("comment"));
            Assert.That(partials3.Contains("header"));
            Assert.That(partials3.Contains("footer"));


            Assert.AreEqual(2, partials2.Count);
            Assert.That(partials2.Contains("header"));
            Assert.That(partials2.Contains("footer"));
        }

        [Test, ExpectedException(typeof(FileNotFoundException))]
        public void FileNotFoundException()
        {
            Expect.Call(viewSourceLoader.HasView("Home\\nosuchfile.spark")).Return(false);
            Expect.Call(viewSourceLoader.HasView("Shared\\nosuchfile.spark")).Return(false);

            mocks.ReplayAll();
            loader.Load("Home", "nosuchfile");
            mocks.VerifyAll();
        }

        [Test]
        public void ExpiresWhenFilesChange()
        {
            ExpectGetSource("home\\changing.spark", new List<Node>());
            mocks.ReplayAll();
            loader.Load("home\\changing.spark");
            Assert.That(loader.IsCurrent());
            _lastModified = 88;
            Assert.That(!loader.IsCurrent());
            mocks.VerifyAll();
        }
    }
}