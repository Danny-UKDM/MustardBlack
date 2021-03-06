using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MustardBlack.Assets;
using MustardBlack.Assets.Css;
using MustardBlack.Assets.Css.Css;
using MustardBlack.Assets.Css.Less;
using MustardBlack.Assets.Css.Sass;
using MustardBlack.Assets.Javascript;
using MustardBlack.Assets.YuiCompressor;

namespace MustardBlack.ViewEngines.Razor.Build
{
	static class Compiler
	{
		public static void Compile(string inPath, string outPath, string assemblyName, IJavascriptPreprocessor javascriptPreprocessor)
		{
			var binPath = Path.Combine(inPath, "bin");
			var webConfigPath = Path.Combine(inPath, "web.config");

			AssemblyRepository.Initialize();
			AssemblyRepository.LoadAssembliesFromPath(binPath);

			var razorConfiguration = new RazorConfiguration(webConfigPath, outPath);
			
			var fileSystem = new BasicFileSystem(inPath);
			var cssPreprocessors = new ICssPreprocessor[] {new CssPreprocessor(), new LessCssPreprocessor(), new SassCssPreprocessor()};
			var razorViewCompiler = new AssetEnrichedRazorViewCompiler(javascriptPreprocessor, cssPreprocessors, fileSystem, new AssetLoader(fileSystem), razorConfiguration);

			var views = Directory.GetFiles(inPath, "*.cshtml", SearchOption.AllDirectories).ToList();
			if (!views.Any())
			{
				Console.WriteLine($"No views found in path '{inPath}'");
				return;
			}
			
			// Find all primary (non-component) view paths which need compiling
			views = views.Select(v =>
			{
				var path = Path.GetDirectoryName(v);
				var name = Path.GetFileNameWithoutExtension(v);
				var ext = Path.GetExtension(v);
				if (name.IndexOf(".", StringComparison.Ordinal) > -1)
					return path + "\\" + name.Substring(0, name.IndexOf(".", StringComparison.Ordinal)) + ext;
				return v;
			}).Distinct().ToList();

			var viewData = new List<RazorViewCompilationData>(views.Count);

			foreach (var view in views)
			{
				var viewPaths = razorViewCompiler.GetViewComponentPaths(view, ".cshtml");
				var jsPaths = razorViewCompiler.GetViewComponentPaths(view, ".js").Where(p => !p.EndsWith(".test.js")).ToArray();
				var cssPaths = razorViewCompiler
					.GetViewComponentPaths(view, ".less")
					.Union(razorViewCompiler.GetViewComponentPaths(view, ".scss"))
					.Union(razorViewCompiler.GetViewComponentPaths(view, ".css"));

				var viewVirtualPath = view.Substring(inPath.Length + 1);

				var areaStartIndex = viewVirtualPath.IndexOf("areas\\", StringComparison.InvariantCultureIgnoreCase) + 6;
				var areaName = viewVirtualPath.Substring(areaStartIndex, viewVirtualPath.IndexOf("\\", areaStartIndex) - areaStartIndex);

				var viewContents = new StringBuilder();

				foreach (var viewFile in viewPaths)
				{
					using (var reader = new StreamReader(new FileStream(viewFile, FileMode.Open, FileAccess.Read)))
					{
						string line;
						while ((line = reader.ReadLine()) != null)
						{
							var trimmedLine = line.Trim();
							if (!string.IsNullOrEmpty(trimmedLine))
							{
								if (trimmedLine.IndexOf("@section") == 0)
									viewContents.AppendLine().AppendLine(trimmedLine);
								else if (trimmedLine.IndexOf("@inherits") == 0)
									viewContents.AppendLine().AppendLine(trimmedLine);
								else
									viewContents.AppendLine(trimmedLine);
							}
						}
					}
				}

				if (jsPaths.Any())
				{
					var jsAssets = new List<AssetContent>();
					foreach (var jsFile in jsPaths)
						jsAssets.Add(new AssetContent(jsFile, File.ReadAllText(jsFile)));
					
					viewContents.Insert(0, razorViewCompiler.PrepareJsForRazorCompilation(jsAssets));
				}

				if (cssPaths.Any())
				{
					var cssBuilder = new StringBuilder();
					foreach (var cssFile in cssPaths)
						cssBuilder.AppendLine(File.ReadAllText(cssFile));

					var css = razorViewCompiler.PrepareCssForRazorCompilation(cssBuilder.ToString(), areaName);
					if(!string.IsNullOrWhiteSpace(css))
						viewContents.Insert(0, css);
				}

				viewData.Add(new RazorViewCompilationData
				{
					Namespace = Path.GetDirectoryName(view.Substring(inPath.Length + 1)).Replace("\\", ".").Replace("/", "."),
					ClassName = RazorViewCompiler.GetSafeClassName(Path.GetFileName(view)),
					FilePath = view,
					ViewContents = viewContents.ToString()
				});
			}

			razorViewCompiler.CompileAndMergeFiles(viewData, assemblyName);
		}
	}
}
