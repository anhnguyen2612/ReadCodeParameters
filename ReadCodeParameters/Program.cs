using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ReadCodeParameters
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                RunMethod(ConfigurationManager.AppSettings["inputDirectory"], ConfigurationManager.AppSettings["outputFile"], ConfigurationManager.AppSettings["FileType"]);
                return;
            }
#region [proccess arguments]

            if (args[0] == "-h")
            {
                ShowInstructions();
                return;
            }

            if (args.Length < 2)
            {
                Console.WriteLine("Error: Missing required arguments.");
                ShowInstructions();
                return;
            }

            string directoryPath = args[0];
            string outputFile = args[1];
            string fileType = args.Length > 2 ? args[2] : "*.cs";

            if (string.IsNullOrEmpty(directoryPath) || string.IsNullOrEmpty(outputFile) || string.IsNullOrEmpty(fileType))
            {
                Console.WriteLine("Error: Input path, output file, and file type must not be empty.");
                ShowInstructions();
                return;
            }
#endregion

            RunMethod(directoryPath, outputFile, fileType);
        }

        private static void RunMethod(string directoryPath, string outputFile, string fileType)
        {
            try
            {
                string res = AnalyzeFilesInDirectorySafeThreadCheck(directoryPath, fileType);
                res = res.Replace("\r\n", "\n");
                string[] data = res.Split('\n');
                FileStreamOptions fileStreamOptions = new FileStreamOptions();
                using (StreamWriter streamWriter = new StreamWriter(outputFile))
                {
                    streamWriter.WriteLine("fileName,className,methodName,variables,lineNumber,dataType");
                    foreach (var line in data)
                    {
                        streamWriter.WriteLine(line);
                    }
                }

                Console.WriteLine("File written successfully.");
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine("Error: Access to the path is denied. " + e.Message);
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine("Error: The specified path is invalid. " + e.Message);
            }
            catch (IOException e)
            {
                Console.WriteLine("Error: An I/O error occurred. " + e.Message);
            }
        }

        #region [proccess arguments function]
        static void ShowInstructions()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  Program <input path> <output file> <file type>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <input path>   The path to the input directory.");
            Console.WriteLine("  <output file>  The path to the output CSV file.");
            Console.WriteLine("  <file type>    The file type to search for (e.g., \"*.cs\").");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h             Show this help message.");
        }

        static void CreateCsvFile(string outputFile)
        {
            // Placeholder method for CSV file creation logic
            // In your actual program, you would implement the logic to create the CSV file here
            Console.WriteLine($"CSV file would be created at: {outputFile}");
        }
        #endregion
        #region [function]
        static string AnalyzeFilesInDirectorySafeThreadCheck(string directoryPath, string fileType)
        {
            StringBuilder res = new StringBuilder();
            string[] preprocessorSymbols = ConfigurationManager.AppSettings["PreprocessorSymbols"].Split(';');
            string[] allFiles = new string[1];
            // get the file attributes for file or directory
            FileAttributes attr = File.GetAttributes(directoryPath);
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                allFiles = Directory.GetFiles(directoryPath, fileType, SearchOption.AllDirectories);
            else
                allFiles[0] = new FileInfo(directoryPath).FullName;

            string pattern = @"^(?!.*\b(site-packages|bin|obj|Designer|Generated|AssemblyInfo|TemporaryGeneratedFile|App|Xaml$|.g.)\b).*" + Regex.Escape(fileType.Replace("*", ""));
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

            //read files
            foreach (string filePath in allFiles)
            {
                if (regex.IsMatch(filePath))
                {
                    string code = File.ReadAllText(filePath);
                    var parseOptions = new CSharpParseOptions().WithPreprocessorSymbols(preprocessorSymbols);
                    var tree = CSharpSyntaxTree.ParseText(code, parseOptions);
                    var root = tree.GetRoot();
                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                    CSharpCompilation compilation = CSharpCompilation.Create("MyCompilation")
                        .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                        .AddSyntaxTrees(tree);
                    
                    var model = compilation.GetSemanticModel(tree);

                    foreach (var classDeclaration in classes)
                    {
                        string className = classDeclaration.Identifier.Text;

                        // Get all field declarations
                        var fieldDeclarations = classDeclaration.Members.OfType<FieldDeclarationSyntax>();
                        // Get all property declarations
                        var propertyDeclarations = classDeclaration.Members.OfType<PropertyDeclarationSyntax>();
                        // Get all local variable declarations
                        var localDeclarations = classDeclaration.DescendantNodes().OfType<LocalDeclarationStatementSyntax>();
                        var methodDeclarations = classDeclaration.Members.OfType<MethodDeclarationSyntax>();
                        Console.WriteLine($"File: {filePath}, Class: {className}");

                        // Process fields
                        foreach (var field in fieldDeclarations)
                        {
                            foreach (var variable in field.Declaration.Variables)
                            {
                                bool isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword);
                                bool isVolatile = field.Modifiers.Any(SyntaxKind.VolatileKeyword);
                                var typeInfo = model.GetTypeInfo(field.Declaration.Type);
                                var type = typeInfo.ConvertedType.ToDisplayString();
                                var lineNumber = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                                string fieldType = isStatic ? "Static Field" : "Field";
                                bool isThreadSafe = isVolatile || isStatic; // A simple heuristic
                                Console.WriteLine($"{fieldType} - Line {lineNumber}: {variable.Identifier.Text}, {type}, Thread-Safe: {isThreadSafe}");
                                res.AppendLine($"{filePath},{className},-,{variable.Identifier.Text},{lineNumber},{type.Replace(',', '|')},{fieldType},{isThreadSafe}");
                            }
                        }

                        // Process properties
                        foreach (var property in propertyDeclarations)
                        {
                            var typeInfo = model.GetTypeInfo(property.Type);
                            var type = typeInfo.ConvertedType.ToDisplayString();
                            var lineNumber = property.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                            // Check for thread safety using common attributes
                            bool isThreadSafe = property.AttributeLists
                                .SelectMany(a => a.Attributes)
                                .Any(a => a.Name.ToString().Contains("ThreadStatic") || a.Name.ToString().Contains("ThreadLocal"));

                            Console.WriteLine($"Property - Line {lineNumber}: {property.Identifier.Text}, {type}, Thread-Safe: {isThreadSafe}");
                            res.AppendLine($"{filePath},{className},-,{property.Identifier.Text},{lineNumber},{type.Replace(',', '|')},Property,{isThreadSafe}");
                        }

                        // Process local variables
                        foreach (var local in localDeclarations)
                        {
                            var variable = local.Declaration.Variables.First();
                            var typeInfo = model.GetTypeInfo(local.Declaration.Type);
                            var type = typeInfo.ConvertedType.ToDisplayString();
                            var lineNumber = local.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                            // Local variables are not thread-safe unless explicitly synchronized in the code
                            bool isThreadSafe = false;

                            Console.WriteLine($"Local Variable - Line {lineNumber}: {variable.Identifier.Text}, {type}, Thread-Safe: {isThreadSafe}");
                            res.AppendLine($"{filePath},{className},-,{variable.Identifier.Text},{lineNumber},{type.Replace(',', '|')},Local Variable,{isThreadSafe}");
                        }

                        // Process variables
                        foreach (var methodDeclaration in methodDeclarations)
                        {
                            var methodName = methodDeclaration.Identifier.Text;
                            var variableDeclarations = methodDeclaration.DescendantNodes().OfType<VariableDeclarationSyntax>();
                            foreach (var variableDeclaration in variableDeclarations)
                            {
                                foreach (var variable in variableDeclaration.Variables)
                                {
                                    var identifier = variable.Identifier.Text;
                                    var typeInfo = model.GetTypeInfo(variableDeclaration.Type);
                                    var type = typeInfo.ConvertedType.ToDisplayString();
                                    var lineNumber = variableDeclaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                                    // Determine if it's static or volatile (for fields)
                                    var parentFieldDeclaration = variableDeclaration.Parent as FieldDeclarationSyntax;
                                    bool isStatic = parentFieldDeclaration?.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) ?? false;
                                    bool isVolatile = parentFieldDeclaration?.Modifiers.Any(m => m.IsKind(SyntaxKind.VolatileKeyword)) ?? false;

                                    // Determine if the property is thread-safe
                                    var parentPropertyDeclaration = variableDeclaration.Parent as PropertyDeclarationSyntax;
                                    bool isThreadSafe = isVolatile || isStatic || (parentPropertyDeclaration != null &&
                                        parentPropertyDeclaration.AttributeLists.SelectMany(a => a.Attributes)
                                        .Any(a => a.Name.ToString().Contains("ThreadStatic") || a.Name.ToString().Contains("ThreadLocal")));

                                    string variableType = isStatic ? "Static Field" : (parentFieldDeclaration != null ? "Field" : "Local Variable");

                                    // Find references to this variable
                                    var references = root.DescendantNodes()
                                        .OfType<IdentifierNameSyntax>()
                                        .Where(id => id.Identifier.Text == identifier)
                                        .Select(id => new
                                        {
                                            LineNumber = id.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                            IsWrite = id.Parent is AssignmentExpressionSyntax assignment && assignment.Left == id
                                        }).ToList();

                                    // Add information about the variable
                                    Console.WriteLine($"{variableType} - Line {lineNumber}: {identifier}, {type}, Thread-Safe: {isThreadSafe}");
                                    res.AppendLine($"{filePath},{className},{methodName},{identifier},{lineNumber},{type.Replace(',', '|')},{variableType},{isThreadSafe}");

                                    // Add information about references
                                    foreach (var reference in references)
                                    {
                                        string operation = reference.IsWrite ? "Write" : "Read";
                                        Console.WriteLine($"    {operation} - Line {reference.LineNumber}");
                                        res.AppendLine($"{filePath},{className},{methodName},{identifier},{reference.LineNumber},{type.Replace(',', '|')},Reference,{isThreadSafe},{operation}");
                                    }
                                }
                            }
                        }

                        Console.WriteLine();
                    }
                }
            }

            return res.ToString();
        }
        #endregion
        #region [old function]
        //static string AnalyzeFilesInDirectory(string directoryPath)
        //{
        //    StringBuilder res = new StringBuilder();
        //        string[] allFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);

        //        string pattern = @"^(?!.*\b(Designer|Generated|AssemblyInfo|TemporaryGeneratedFile|App|Xaml)\b).*\.cs$";
        //        Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
        //        foreach (string filePath in allFiles)
        //        {
        //            if (regex.IsMatch(filePath))
        //            {
        //                var code = File.ReadAllText(filePath);
        //                var tree = CSharpSyntaxTree.ParseText(code);
        //                var root = tree.GetRoot();
        //                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        //                var compilation = CSharpCompilation.Create("MyCompilation")
        //                    .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
        //                    .AddSyntaxTrees(tree);
        //                var model = compilation.GetSemanticModel(tree);

        //                foreach (var classDeclaration in classes)
        //                {
        //                    var className = classDeclaration.Identifier.Text;

        //                    // Get all field declarations
        //                    var fieldDeclarations = classDeclaration.Members.OfType<FieldDeclarationSyntax>();
        //                    // Get all property declarations
        //                    var propertyDeclarations = classDeclaration.Members.OfType<PropertyDeclarationSyntax>();
        //                    // Get all local variable declarations
        //                    var localDeclarations = classDeclaration.DescendantNodes().OfType<LocalDeclarationStatementSyntax>();

        //                    Console.WriteLine($"File: {filePath}, Class: {className}");

        //                    // Process fields
        //                    foreach (var field in fieldDeclarations)
        //                    {
        //                        foreach (var variable in field.Declaration.Variables)
        //                        {
        //                            bool isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword);
        //                            var typeInfo = model.GetTypeInfo(field.Declaration.Type);
        //                            var type = typeInfo.ConvertedType.ToDisplayString();
        //                            var lineNumber = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        //                            string fieldType = isStatic ? "Static Field" : "Field";
        //                            Console.WriteLine($"{fieldType} - Line {lineNumber}: {variable.Identifier.Text}, {type}");
        //                            res.AppendLine($"{filePath},{className},{variable.Identifier.Text},{type.Replace(',', '|')},{lineNumber},{fieldType}");
        //                        }
        //                    }

        //                    // Process properties
        //                    foreach (var property in propertyDeclarations)
        //                    {
        //                        var typeInfo = model.GetTypeInfo(property.Type);
        //                        var type = typeInfo.ConvertedType.ToDisplayString();
        //                        var lineNumber = property.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        //                        Console.WriteLine($"Property - Line {lineNumber}: {property.Identifier.Text}, {type}");
        //                        res.AppendLine($"{filePath},{className},{property.Identifier.Text},{type.Replace(',', '|')},{lineNumber},Property");
        //                    }

        //                    // Process local variables
        //                    foreach (var local in localDeclarations)
        //                    {
        //                        var variable = local.Declaration.Variables.First();
        //                        var typeInfo = model.GetTypeInfo(local.Declaration.Type);
        //                        var type = typeInfo.ConvertedType.ToDisplayString();
        //                        var lineNumber = local.GetLocation().GetLineSpan().StartLinePosition.Line + 1; // Adding 1 because line numbers start from 0
        //                        Console.WriteLine($"Local Variable - Line {lineNumber}: {variable.Identifier.Text}, {type}");
        //                        res.AppendLine($"{filePath},{className},{variable.Identifier.Text},{type.Replace(',', '|')},{lineNumber},Local Variable");
        //                    }

        //                    Console.WriteLine();
        //                }
        //            }
        //        }
        //    return res.ToString();
        //}
        #endregion
    }
}
