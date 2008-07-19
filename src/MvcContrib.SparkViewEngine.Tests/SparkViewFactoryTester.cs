using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using MvcContrib.SparkViewEngine;
using MvcContrib.UnitTests.SparkViewEngine.Models;
using MvcContrib.ViewFactories;
using NUnit.Framework;
using Rhino.Mocks;
using Spark;

namespace MvcContrib.UnitTests.SparkViewEngine
{
	[TestFixture, Category("SparkViewEngine")]
	public class SparkViewFactoryTester
	{
		private MockRepository mocks;
		private HttpContextBase context;
		private HttpRequestBase request;
		private HttpResponseBase response;
		private IController controller;
		private RouteData routeData;

		private SparkViewFactory factory;

		[SetUp]
		public void Init()
		{
			// clears cache
			CompiledViewHolder.Current = null;

			// reset routes
			RouteTable.Routes.Clear();
			RouteTable.Routes.Add(new Route("{controller}/{action}/{id}", new MvcRouteHandler())
									  {
										  Defaults = new RouteValueDictionary(new { action = "Index", id = "" })
									  });

			mocks = new MockRepository();
			context = mocks.DynamicHttpContextBase();
			response = context.Response;
			request = context.Request;
			SetupResult.For(request.ApplicationPath).Return("/");
			SetupResult.For(response.ApplyAppPathModifier("")).IgnoreArguments().Do(new Func<string, string>(path => path));

			Expect.Call(() => response.Write(""))
				.IgnoreArguments()
				.Do(new writedelegate(onwrite));

			//            SetupResult.For(delegate() { response.Write(null); }).IgnoreArguments().Callback(onwrite);

			controller = mocks.DynamicMock<IController>();
			sb = new StringBuilder();

			routeData = new RouteData();
			routeData.Values.Add("controller", "Home");
			routeData.Values.Add("action", "Index");

			factory = new SparkViewFactory(new FileSystemViewSourceLoader("Views"));

		}
		delegate void writedelegate(string data);
		private StringBuilder sb;

		void onwrite(string data)
		{
			sb.Append(data);
		}

		ViewContext MakeViewContext(string viewName, string masterName)
		{
			return MakeViewContext(viewName, masterName, null);
		}

		ViewContext MakeViewContext(string viewName, string masterName, object viewData)
		{
			return new ViewContext(context, routeData, controller, viewName, masterName, new ViewDataDictionary(viewData), null);
		}


		[Test]
		public void RenderPlainView()
		{
			mocks.ReplayAll();

			factory.RenderView(MakeViewContext("index", null));

			mocks.VerifyAll();
		}


		[Test]
		public void ForEachTest()
		{
			mocks.ReplayAll();

			factory.RenderView(MakeViewContext("foreach", null));

			mocks.VerifyAll();

			string content = sb.ToString();
			Assert.That(content.Contains(@"<li class=""odd"">1: foo</li>"));
			Assert.That(content.Contains(@"<li class=""even"">2: bar</li>"));
			Assert.That(content.Contains(@"<li class=""odd"">3: baaz</li>"));
		}


		[Test]
		public void GlobalSetTest()
		{

			mocks.ReplayAll();

			factory.RenderView(MakeViewContext("globalset", null));

			mocks.VerifyAll();

			string content = sb.ToString();
			Assert.That(content.Contains("<p>default: Global set test</p>"));
			Assert.That(content.Contains("<p>7==7</p>"));
		}

		[Test]
		public void MasterTest()
		{
			mocks.ReplayAll();

			factory.RenderView(MakeViewContext("childview", "layout"));

			mocks.VerifyAll();
			string content = sb.ToString();
			Assert.That(content.Contains("<title>Standalone Index View</title>"));
			Assert.That(content.Contains("<h1>Standalone Index View</h1>"));
			Assert.That(content.Contains("<p>no header by default</p>"));
			Assert.That(content.Contains("<p>no footer by default</p>"));
		}

		[Test]
		public void CaptureNamedContent()
		{

			mocks.ReplayAll();

			factory.RenderView(MakeViewContext("namedcontent", "layout"));

			mocks.VerifyAll();
			string content = sb.ToString();
			Assert.That(content.Contains("<p>main content</p>"));
			Assert.That(content.Contains("<p>this is the header</p>"));
			Assert.That(content.Contains("<p>footer part one</p>"));
			Assert.That(content.Contains("<p>footer part two</p>"));
		}

		[Test]
		public void HtmlHelperWorksOnItsOwn()
		{
			mocks.ReplayAll();

			var viewContext = MakeViewContext("helpers", null);
			var html = new HtmlHelper(viewContext, new ViewDataContainer { ViewData = viewContext.ViewData });
			var link = html.ActionLink("hello", "world");
			response.Write(link);

			mocks.VerifyAll();

			Assert.AreEqual("<a href=\"/Home/world\">hello</a>", link);
		}

		class ViewDataContainer : IViewDataContainer
		{
			public ViewDataDictionary ViewData { get; set; }
		}


		[Test]
		public void UsingHtmlHelper()
		{

			mocks.ReplayAll();

			factory.RenderView(MakeViewContext("helpers", null));

			mocks.VerifyAll();
			string content = sb.ToString();
			Assert.That(content.Contains("<p><a href=\"/Home/Sort\">Click me</a></p>"));
			Assert.That(content.Contains("<p>foo&gt;bar</p>"));
		}

		[Test]
		public void UsingPartialFile()
		{
			mocks.ReplayAll();

			factory.RenderView(MakeViewContext("usingpartial", null));

			mocks.VerifyAll();
			string content = sb.ToString();
			Assert.That(content.Contains("<li>Partial where x=\"zero\"</li>"));
			Assert.That(content.Contains("<li>Partial where x=\"one\"</li>"));
			Assert.That(content.Contains("<li>Partial where x=\"two\"</li>"));
			Assert.That(content.Contains("<li>Partial where x=\"three\"</li>"));
			Assert.That(content.Contains("<li>Partial where x=\"four\"</li>"));
		}

		[Test]
		public void UsingPartialFileImplicit()
		{
			mocks.ReplayAll();

			factory.RenderView(MakeViewContext("usingpartialimplicit", null));

			mocks.VerifyAll();
			string content = sb.ToString();
			Assert.That(content.Contains("<li class=\"odd\">one</li>"));
			Assert.That(content.Contains("<li class=\"even\">two</li>"));
		}


		[Test]
		public void DeclaringViewDataAccessor()
		{
			mocks.ReplayAll();
			var comments = new[] { new Comment { Text = "foo" }, new Comment { Text = "bar" } };
			var viewContext = MakeViewContext("viewdata", null, new { Comments = comments, Caption = "Hello world" });

			factory.RenderView(viewContext);
            
			mocks.VerifyAll();
			string content = sb.ToString();
			Assert.That(content.Contains("<h1>Hello world</h1>"));
			Assert.That(content.Contains("<p>foo</p>"));
			Assert.That(content.Contains("<p>bar</p>"));
		}



		[Test]
		public void UsingNamespace()
		{
			mocks.ReplayAll();
			var viewContext = MakeViewContext("usingnamespace", null);

			factory.RenderView(viewContext);

			mocks.VerifyAll();
			string content = sb.ToString();
			Assert.That(content.Contains("<p>Foo</p>"));
			Assert.That(content.Contains("<p>Bar</p>"));
			Assert.That(content.Contains("<p>Hello</p>"));
		}

		[Test]
		public void ViewSourceLoaderCanBeChanged()
		{
			IViewSourceLoader replacement = mocks.DynamicMock<IViewSourceLoader>();

			mocks.ReplayAll();

			IViewSourceLoader existing = factory.ViewSourceLoader;
			Assert.AreNotSame(existing, replacement);
			Assert.AreSame(existing, factory.ViewSourceLoader);

			factory.ViewSourceLoader = replacement;
			Assert.AreSame(replacement, factory.ViewSourceLoader);
			Assert.AreNotSame(existing, factory.ViewSourceLoader);
		}

		[Test]
		public void NullViewDataIsSafe()
		{
			mocks.ReplayAll();
			var viewContext = new ViewContext(context, routeData, controller, "viewdatanull", null, null, null);

			factory.RenderView(viewContext);
			mocks.VerifyAll();

			string content = sb.ToString();
			Assert.That(content.Contains("<p>nothing</p>"));
		}

		[Test]
		public void ViewDataWithModel()
		{
			mocks.ReplayAll();
			var viewContext = MakeViewContext("viewdatamodel", null, new Comment {Text = "Hello"});
			factory.RenderView(viewContext);
			mocks.VerifyAll();

			string content = sb.ToString();
			Assert.That(content.Contains("<p>Hello</p>"));
		}
	}
}
