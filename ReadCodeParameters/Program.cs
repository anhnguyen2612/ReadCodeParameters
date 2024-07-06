﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReadCodeParameters
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string directoryPath = @"D:\code\DU\V0412_TobuSeibu\DSRDispUnit\ExeModule"; // Replace with your directory path
            string res = AnalyzeFilesInDirectorySafeThreadCheck(directoryPath);
            res = res.Replace("\r\n", "\n");
            string[] data = res.Split('\n');

            try
            {
                using (StreamWriter streamWriter = new StreamWriter(@"D:\variable.csv"))
                {
                    streamWriter.WriteLine("fileName,className,Variables,dataType,lineNumber,modifier");
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

            Console.ReadLine();
        }

    static string AnalyzeFilesInDirectory(string directoryPath)
    {
        StringBuilder res = new StringBuilder();
            string[] allFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);

            string pattern = @"^(?!.*\b(Designer|Generated|AssemblyInfo|TemporaryGeneratedFile|App|Xaml)\b).*\.cs$";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            foreach (string filePath in allFiles)
            {
                if (regex.IsMatch(filePath))
                {
                    var code = File.ReadAllText(filePath);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = tree.GetRoot();
                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                    var compilation = CSharpCompilation.Create("MyCompilation")
                        .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                        .AddSyntaxTrees(tree);
                    var model = compilation.GetSemanticModel(tree);

                    foreach (var classDeclaration in classes)
                    {
                        var className = classDeclaration.Identifier.Text;

                        // Get all field declarations
                        var fieldDeclarations = classDeclaration.Members.OfType<FieldDeclarationSyntax>();
                        // Get all property declarations
                        var propertyDeclarations = classDeclaration.Members.OfType<PropertyDeclarationSyntax>();
                        // Get all local variable declarations
                        var localDeclarations = classDeclaration.DescendantNodes().OfType<LocalDeclarationStatementSyntax>();

                        Console.WriteLine($"File: {filePath}, Class: {className}");

                        // Process fields
                        foreach (var field in fieldDeclarations)
                        {
                            foreach (var variable in field.Declaration.Variables)
                            {
                                bool isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword);
                                var typeInfo = model.GetTypeInfo(field.Declaration.Type);
                                var type = typeInfo.ConvertedType.ToDisplayString();
                                var lineNumber = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                                string fieldType = isStatic ? "Static Field" : "Field";
                                Console.WriteLine($"{fieldType} - Line {lineNumber}: {variable.Identifier.Text}, {type}");
                                res.AppendLine($"{filePath},{className},{variable.Identifier.Text},{type.Replace(',', '|')},{lineNumber},{fieldType}");
                            }
                        }

                        // Process properties
                        foreach (var property in propertyDeclarations)
                        {
                            var typeInfo = model.GetTypeInfo(property.Type);
                            var type = typeInfo.ConvertedType.ToDisplayString();
                            var lineNumber = property.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                            Console.WriteLine($"Property - Line {lineNumber}: {property.Identifier.Text}, {type}");
                            res.AppendLine($"{filePath},{className},{property.Identifier.Text},{type.Replace(',', '|')},{lineNumber},Property");
                        }

                        // Process local variables
                        foreach (var local in localDeclarations)
                        {
                            var variable = local.Declaration.Variables.First();
                            var typeInfo = model.GetTypeInfo(local.Declaration.Type);
                            var type = typeInfo.ConvertedType.ToDisplayString();
                            var lineNumber = local.GetLocation().GetLineSpan().StartLinePosition.Line + 1; // Adding 1 because line numbers start from 0
                            Console.WriteLine($"Local Variable - Line {lineNumber}: {variable.Identifier.Text}, {type}");
                            res.AppendLine($"{filePath},{className},{variable.Identifier.Text},{type.Replace(',', '|')},{lineNumber},Local Variable");
                        }

                        Console.WriteLine();
                    }
                }
            }
        return res.ToString();
    }

    static string AnalyzeFilesInDirectorySafeThreadCheck(string directoryPath)
        {
            StringBuilder res = new StringBuilder();
            string[] allFiles = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);

            string pattern = @"^(?!.*\b(obj|Designer|Generated|AssemblyInfo|TemporaryGeneratedFile|App|Xaml)\b).*\.cs$";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            foreach (string filePath in allFiles)
            {
                if (regex.IsMatch(filePath))
                {
                    var code = File.ReadAllText(filePath);
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var root = tree.GetRoot();
                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                    var compilation = CSharpCompilation.Create("MyCompilation")
                        .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                        .AddSyntaxTrees(tree);
                    var model = compilation.GetSemanticModel(tree);

                    foreach (var classDeclaration in classes)
                    {
                        var className = classDeclaration.Identifier.Text;

                        // Get all field declarations
                        var fieldDeclarations = classDeclaration.Members.OfType<FieldDeclarationSyntax>();
                        // Get all property declarations
                        var propertyDeclarations = classDeclaration.Members.OfType<PropertyDeclarationSyntax>();
                        // Get all local variable declarations
                        var localDeclarations = classDeclaration.DescendantNodes().OfType<LocalDeclarationStatementSyntax>();
                        var variableDeclarations = classDeclaration.DescendantNodes().OfType<VariableDeclarationSyntax>();

                        Console.WriteLine($"File: {filePath}, Class: {className}");

                        //// Process fields
                        //foreach (var field in fieldDeclarations)
                        //{
                        //    foreach (var variable in field.Declaration.Variables)
                        //    {
                        //        bool isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword);
                        //        bool isVolatile = field.Modifiers.Any(SyntaxKind.VolatileKeyword);
                        //        var typeInfo = model.GetTypeInfo(field.Declaration.Type);
                        //        var type = typeInfo.ConvertedType.ToDisplayString();
                        //        var lineNumber = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                        //        string fieldType = isStatic ? "Static Field" : "Field";
                        //        bool isThreadSafe = isVolatile || isStatic; // A simple heuristic
                        //        Console.WriteLine($"{fieldType} - Line {lineNumber}: {variable.Identifier.Text}, {type}, Thread-Safe: {isThreadSafe}");
                        //        res.AppendLine($"{filePath},{className},{variable.Identifier.Text},{type.Replace(',', '|')},{lineNumber},{fieldType},{isThreadSafe}");
                        //    }
                        //}

                        //// Process properties
                        //foreach (var property in propertyDeclarations)
                        //{
                        //    var typeInfo = model.GetTypeInfo(property.Type);
                        //    var type = typeInfo.ConvertedType.ToDisplayString();
                        //    var lineNumber = property.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                        //    // Check for thread safety using common attributes
                        //    bool isThreadSafe = property.AttributeLists
                        //        .SelectMany(a => a.Attributes)
                        //        .Any(a => a.Name.ToString().Contains("ThreadStatic") || a.Name.ToString().Contains("ThreadLocal"));

                        //    Console.WriteLine($"Property - Line {lineNumber}: {property.Identifier.Text}, {type}, Thread-Safe: {isThreadSafe}");
                        //    res.AppendLine($"{filePath},{className},{property.Identifier.Text},{type.Replace(',', '|')},{lineNumber},Property,{isThreadSafe}");
                        //}

                        //// Process local variables
                        //foreach (var local in localDeclarations)
                        //{
                        //    var variable = local.Declaration.Variables.First();
                        //    var typeInfo = model.GetTypeInfo(local.Declaration.Type);
                        //    var type = typeInfo.ConvertedType.ToDisplayString();
                        //    var lineNumber = local.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                        //    // Local variables are not thread-safe unless explicitly synchronized in the code
                        //    bool isThreadSafe = false;

                        //    Console.WriteLine($"Local Variable - Line {lineNumber}: {variable.Identifier.Text}, {type}, Thread-Safe: {isThreadSafe}");
                        //    res.AppendLine($"{filePath},{className},{variable.Identifier.Text},{type.Replace(',', '|')},{lineNumber},Local Variable,{isThreadSafe}");
                        //}

                        // Process variables
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
                                res.AppendLine($"{filePath},{className},{identifier},{type.Replace(',', '|')},{lineNumber},{variableType},{isThreadSafe}");

                                // Add information about references
                                foreach (var reference in references)
                                {
                                    string operation = reference.IsWrite ? "Write" : "Read";
                                    Console.WriteLine($"    {operation} - Line {reference.LineNumber}");
                                    res.AppendLine($"{filePath},{className},{identifier},{type.Replace(',', '|')},{reference.LineNumber},Reference,{operation}");
                                }
                            }
                        }

                        Console.WriteLine();
                    }
                }
            }

            return res.ToString();
        }


    }
}