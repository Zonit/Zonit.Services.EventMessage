using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace Zonit.Messaging.Tasks.SourceGenerators;

/// <summary>
/// Source Generator który automatycznie generuje rejestracjê handlerów zadañ dla AOT/Trimming.
/// Skanuje projekt w poszukiwaniu klas dziedzicz¹cych po TaskHandler&lt;T&gt; lub TaskBase&lt;T&gt; i generuje:
/// 1. Extension method AddTaskHandlers() dla automatycznej rejestracji wszystkich handlerów
/// 2. AOT-safe subscription bez reflection
/// </summary>
[Generator]
public class TaskHandlerGenerator : IIncrementalGenerator
{
    // New API
    private const string TaskHandlerClassName = "TaskHandler";
    private const string TaskHandlerNamespace = "Zonit.Messaging.Tasks";
    
    // Legacy API
    private const string TaskBaseClassName = "TaskBase";
    private const string TaskBaseNamespace = "Zonit.Services.EventMessage";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ZnajdŸ wszystkie klasy dziedzicz¹ce po TaskBase<T>
        var handlerClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateHandlerClass(node),
                transform: static (ctx, _) => GetHandlerInfo(ctx))
            .Where(static info => info is not null);

        // Zbierz wszystkie handlery i wygeneruj kod
        var compilation = context.CompilationProvider.Combine(handlerClasses.Collect());

        context.RegisterSourceOutput(compilation, static (spc, source) => Execute(source.Left, source.Right!, spc));
    }

    private static bool IsCandidateHandlerClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl
               && classDecl.BaseList is not null
               && classDecl.BaseList.Types.Any();
    }

    private static TaskHandlerInfo? GetHandlerInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);

        if (symbol is not INamedTypeSymbol classSymbol)
            return null;

        // SprawdŸ czy klasa jest abstract lub static
        if (classSymbol.IsAbstract || classSymbol.IsStatic)
            return null;

        // ZnajdŸ klasê bazow¹ TaskHandler<T> (new API) lub TaskBase<T> (legacy)
        var baseType = classSymbol.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.TypeArguments.Length == 1)
            {
                var baseTypeName = baseType.Name;
                var baseTypeNamespace = baseType.ContainingNamespace?.ToDisplayString();
                
                // Check for new TaskHandler<T> API
                bool isNewApi = baseTypeName == TaskHandlerClassName 
                    && baseTypeNamespace == TaskHandlerNamespace;
                
                // Check for legacy TaskBase<T> API
                bool isLegacyApi = baseTypeName == TaskBaseClassName 
                    && baseTypeNamespace == TaskBaseNamespace;
                
                if (isNewApi || isLegacyApi)
                {
                    var taskType = baseType.TypeArguments[0];
                    
                    return new TaskHandlerInfo(
                        HandlerFullName: classSymbol.ToDisplayString(),
                        HandlerName: classSymbol.Name,
                        TaskFullName: taskType.ToDisplayString(),
                        TaskName: taskType.Name,
                        Namespace: classSymbol.ContainingNamespace?.ToDisplayString() ?? "Global",
                        IsLegacy: isLegacyApi
                    );
                }
            }
            baseType = baseType.BaseType;
        }

        return null;
    }






    private static void Execute(Compilation compilation, ImmutableArray<TaskHandlerInfo?> handlers, SourceProductionContext context)
    {
        var validHandlers = handlers
            .Where(h => h is not null)
            .Cast<TaskHandlerInfo>()
            .Distinct()
            .ToList();

        // Get assembly name for namespace - use root namespace from assembly name
        var assemblyName = compilation.AssemblyName ?? "Unknown";
        var targetNamespace = assemblyName;

        // Scan referenced assemblies for TaskHandlerExtensions classes
        var referencedRegistrations = FindReferencedTaskRegistrations(compilation);

        // Generate local registration if we have handlers
        if (validHandlers.Count > 0)
        {
            var registrationSource = GenerateRegistrationExtensions(validHandlers, targetNamespace);
            context.AddSource("TaskHandlerRegistration.g.cs", registrationSource);
        }

        // Generate global registration only if there's something to register
        // (either local handlers or referenced registrations)
        if (validHandlers.Count > 0 || referencedRegistrations.Count > 0)
        {
            var globalSource = GenerateGlobalRegistration(targetNamespace, validHandlers.Count > 0, referencedRegistrations);
            context.AddSource("TaskHandlerGlobalRegistration.g.cs", globalSource);
        }
    }

    private static List<string> FindReferencedTaskRegistrations(Compilation compilation)
    {
        var registrations = new List<string>();

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                FindTaskHandlerExtensionsClasses(assemblySymbol.GlobalNamespace, registrations);
            }
        }

        return registrations.Distinct().ToList();
    }

    private static void FindTaskHandlerExtensionsClasses(INamespaceSymbol namespaceSymbol, List<string> registrations)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            if (type.Name == "TaskHandlerExtensions" && type.IsStatic)
            {
                var hasMethod = type.GetMembers("AddTaskHandlers")
                    .OfType<IMethodSymbol>()
                    .Any(m => m.IsExtensionMethod && m.Parameters.Length == 1);
                
                if (hasMethod)
                {
                    var ns = type.ContainingNamespace?.ToDisplayString();
                    if (!string.IsNullOrEmpty(ns) && ns != "Global")
                    {
                        registrations.Add(ns);
                    }
                }
            }
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            FindTaskHandlerExtensionsClasses(nestedNamespace, registrations);
        }
    }

    private static string GenerateGlobalRegistration(string targetNamespace, bool hasLocalHandlers, List<string> referencedNamespaces)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();

        foreach (var ns in referencedNamespaces.OrderBy(n => n))
        {
            if (ns != targetNamespace)
            {
                sb.AppendLine($"using {ns};");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {targetNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated global registration for all task handlers.");
        sb.AppendLine("/// Registers handlers from this assembly and all referenced assemblies.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class TaskHandlerGlobalExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all task handlers from this assembly and all referenced assemblies.");
        sb.AppendLine("    /// This is a convenience method that calls AddTaskHandlers() for each assembly.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddAllTaskHandlers(this IServiceCollection services)");
        sb.AppendLine("    {");

        if (hasLocalHandlers)
        {
            sb.AppendLine($"        services.AddTaskHandlers(); // Local handlers");
        }

        foreach (var ns in referencedNamespaces.OrderBy(n => n))
        {
            sb.AppendLine($"        {ns}.TaskHandlerExtensions.AddTaskHandlers(services); // From {ns}");
        }

        if (!hasLocalHandlers && referencedNamespaces.Count == 0)
        {
            sb.AppendLine("        // No task handlers discovered in this assembly or referenced assemblies");
        }

        sb.AppendLine();
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateRegistrationExtensions(List<TaskHandlerInfo> handlers, string targetNamespace)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine("using Microsoft.Extensions.Hosting;");
        sb.AppendLine("using Zonit.Messaging.Tasks;");
        sb.AppendLine("using Zonit.Messaging.Tasks.Hosting;");
        
        // Only add legacy using if there are legacy handlers
        if (handlers.Any(h => h.IsLegacy))
        {
            sb.AppendLine("using Zonit.Services.EventMessage;");
        }
        sb.AppendLine();

        // Dodaj using dla ka¿dego namespace handlera (tylko jeœli ró¿ny od target namespace)
        var namespaces = handlers.Select(h => h.Namespace).Distinct().OrderBy(n => n);
        foreach (var ns in namespaces)
        {
            if (ns != "Zonit.Messaging.Tasks" && ns != "Zonit.Services.EventMessage" && ns != "Global" && ns != targetNamespace)
            {
                sb.AppendLine($"using {ns};");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {targetNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated extension methods for registering task handlers.");
        sb.AppendLine("/// This class is generated by TaskHandlerGenerator.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class TaskHandlerExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all discovered task handlers (TaskHandler&lt;T&gt;) in the DI container.");
        sb.AppendLine("    /// This method is AOT/Trimming safe - no reflection at runtime.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddTaskHandlers(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Register task provider services");
        sb.AppendLine("        services.TryAddSingleton<ITaskManager, TaskManager>();");
        sb.AppendLine("        services.TryAddSingleton<ITaskProvider, TaskProvider>();");
        sb.AppendLine("        services.AddHostedService<TaskHandlerRegistrationHostedService>();");
        sb.AppendLine();

        foreach (var handler in handlers)
        {
            sb.AppendLine($"        // Handler: {handler.HandlerName} for task: {handler.TaskName}{(handler.IsLegacy ? " [LEGACY]" : "")}");
            sb.AppendLine($"        services.AddScoped<{handler.HandlerFullName}>();");
            sb.AppendLine($"        services.AddScoped<ITaskHandler<{handler.TaskFullName}>>(sp => sp.GetRequiredService<{handler.HandlerFullName}>());");
            sb.AppendLine($"        services.AddSingleton<TaskHandlerRegistration>(new TaskHandlerRegistration<{handler.TaskFullName}>());");
            sb.AppendLine();
        }


        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the list of all discovered task handler types for AOT-safe subscription.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static System.Type[] GetTaskHandlerTypes()");
        sb.AppendLine("    {");
        sb.AppendLine("        return new System.Type[]");
        sb.AppendLine("        {");

        foreach (var handler in handlers)
        {
            sb.AppendLine($"            typeof({handler.HandlerFullName}),");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private record TaskHandlerInfo(
        string HandlerFullName,
        string HandlerName,
        string TaskFullName,
        string TaskName,
        string Namespace,
        bool IsLegacy
    );
}
