﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace IFC.Generate
{
	class Program
	{
		static void Main(string[] args)
		{
			if(args.Length != 2)
			{
				Console.WriteLine("The syntax for the command is:");
				Console.WriteLine("IFC-gen <express schema path> <output directory>");
				return;
			}

			var expressPath = args[0];
			if(!File.Exists(expressPath))
			{
				Console.WriteLine($"The specified express file path, {expressPath}, does not exist.");
				return;
			}

			var outputDir = args[1];
			if(!Directory.Exists(outputDir))
			{
				Console.WriteLine($"The specified output directory, {outputDir}, does not exist.");
				return;
			}

			using (FileStream fs = new FileStream(expressPath, FileMode.Open))
			{
				var input = new AntlrInputStream(fs);
				var lexer = new Express.ExpressLexer(input);
				var tokens = new CommonTokenStream(lexer);

				var parser = new Express.ExpressParser(tokens);
				parser.BuildParseTree = true;

				var tree = parser.schemaDeclaration();
				var walker = new ParseTreeWalker();
				var listener = new Express.ExpressListener();
				walker.Walk(listener, tree);

				var sb = new StringBuilder();
				// Write types, excluding selects.
				foreach(var t in listener.Types)
				{
					sb.Append(t.ToString());
				}

				//Write entities.
				foreach(var e in listener.Entities)
				{	
					if(e.SubtypeOf.Any())
					{
						e.ParentType = listener.Entities.First(ent=>ent.Name == e.SubtypeOf.First());
					}
					
					sb.Append(e.ToString());
				}
				var types = 
$@"/*
This code was generated by a tool. DO NOT MODIFY this code manually, unless you really know what you are doing.
 */
using System;
using System.Collections.Generic;
				
namespace IFC4
{{
{sb.ToString()}
}}";			
				var outPath = Path.Combine(outputDir, "IFC.cs");
				File.WriteAllText(outPath,types);
				
				var maxSelectParams = listener.Types
										.Where(t=>t.TypeInfo is Express.SelectInfo)
										.Select(t=>t.TypeInfo).Cast<Express.SelectInfo>()
										.Max(si=>si.Values.Count());

				var selectConstructors = new StringBuilder();
				for (var i=2; i<=maxSelectParams; i++)
				{
					selectConstructors.AppendLine(WriteSelect(i));
				}
				var selects =
$@"/*
/*
This code was generated by a tool. DO NOT MODIFY this code manually, unless you really know what you are doing.
*/
using System.Collections.Generic;

namespace IFC4
{{
	public abstract class Select
	{{
		public dynamic Value {{get;protected set;}}
	}}
	{selectConstructors.ToString()}
}}";
				outPath = Path.Combine(outputDir, "IfcSelect.cs");
				File.WriteAllText(outPath,selects);

				/*var tokenStr = new StringBuilder();
				foreach(var t in tokens.GetTokens())
				{
					tokenStr.AppendLine(t.ToString());
				}
				File.WriteAllText("tokens.txt",tokenStr.ToString());*/
			}
			
		}

		private static string WriteSelect(int size)
		{
			var constructorBuilder = new StringBuilder();
			for(var i=1; i<=size; i++)
			{
				constructorBuilder.AppendLine($"\t\tpublic IfcSelect(T{i} value){{Value = value;}}");
			}
			var genParams = new List<string>();
			for(var i=0; i<size; i++)
			{
				genParams.Add("T"+(i+1));
			}
			var select=
$@"
	public abstract class IfcSelect<{string.Join(",",genParams)}> : Select
	{{
{constructorBuilder.ToString()}
	}}"
;
			return select;
		}
	}
}
