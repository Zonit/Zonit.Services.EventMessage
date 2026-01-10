using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace Zonit.Messaging.Events.SourceGenerators;

/// <summary>
/// Source Generator który automatycznie generuje rejestracjê handlerów eventów dla AOT/Trimming.
/// Skanuje projekt w poszukiwaniu klas implementuj¹cych IEventHandler&lt;T&gt; i generuje:
/// 1. Extension method AddEventHandlers() dla automatycznej rejestracji wszystkich handlerów
/// 2. AOT-safe subscription bez reflection
/// </summary>
[Generator]
public class EventHandlerGenerator : IIncrementalGenerator
{
    private const string EventHandlerInterfaceName = "IEventHandler";
    private const string EventHandlerNamespace = "Zonit.Messaging.Events";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ZnajdŸ wszystkie klasy dziedzicz¹ce po EventBase<T>
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

    private static EventHandlerInfo? GetHandlerInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);

        if (symbol is not INamedTypeSymbol classSymbol)
            return null;

        // SprawdŸ czy klasa jest abstract lub static
        if (classSymbol.IsAbstract || classSymbol.IsStatic)
            return null;

        // Check for IEventHandler<T> interface (new API only)
        // Legacy EventBase<T> uses different registration system and should not be detected here
        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.IsGenericType 
                && iface.Name == EventHandlerInterfaceName
                && iface.ContainingNamespace?.ToDisplayString() == EventHandlerNamespace
                && iface.TypeArguments.Length == 1)
            {
                var eventType = iface.TypeArguments[0];
                
                return new EventHandlerInfo(
                    HandlerFullName: classSymbol.ToDisplayString(),
                    HandlerName: classSymbol.Name,
                    EventFullName: eventType.ToDisplayString(),
                    EventName: eventType.Name,
                    Namespace: classSymbol.ContainingNamespace?.ToDisplayString() ?? "Global"
                );
            }
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<EventHandlerInfo?> handlers, SourceProductionContext context)
    {
        var validHandlers = handlers
            .Where(h => h is not null)
            .Cast<EventHandlerInfo>()
            .Distinct()
            .ToList();

        // Get assembly name for namespace - use root namespace from assembly name
        var assemblyName = compilation.AssemblyName ?? "Unknown";
        
        // Use assembly name as namespace (e.g., "Kemavo.Plugins.Catalogs.Application" -> "Kemavo.Plugins.Catalogs.Application")
        var targetNamespace = assemblyName;

        // Scan referenced assemblies for EventHandlerExtensions classes
        var referencedRegistrations = FindReferencedEventRegistrations(compilation);

        // Generate local registration if we have handlers
        if (validHandlers.Count > 0)
        {
            var registrationSource = GenerateRegistrationExtensions(validHandlers, targetNamespace);
            context.AddSource("EventHandlerRegistration.g.cs", registrationSource);
        }

        // Always generate global registration method (calls all discovered registrations)
        var globalSource = GenerateGlobalRegistration(targetNamespace, validHandlers.Count > 0, referencedRegistrations);
        context.AddSource("EventHandlerGlobalRegistration.g.cs", globalSource);
    }

    private static List<string> FindReferencedEventRegistrations(Compilation compilation)
    {
        var registrations = new List<string>();

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                // Look for EventHandlerExtensions class with AddEventHandlers method
                FindEventHandlerExtensionsClasses(assemblySymbol.GlobalNamespace, registrations);
            }
        }

        return registrations.Distinct().ToList();
    }

    private static void FindEventHandlerExtensionsClasses(INamespaceSymbol namespaceSymbol, List<string> registrations)
    {
        // Check types in this namespace
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            if (type.Name == "EventHandlerExtensions" && type.IsStatic)
            {
                // Check if it has AddEventHandlers method
                var hasMethod = type.GetMembers("AddEventHandlers")
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

        // Recursively check nested namespaces
        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            FindEventHandlerExtensionsClasses(nestedNamespace, registrations);
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

        // Add using for each referenced namespace
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
        sb.AppendLine("/// Auto-generated global registration for all event handlers.");
        sb.AppendLine("/// Registers handlers from this assembly and all referenced assemblies.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class EventHandlerGlobalExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all event handlers from this assembly and all referenced assemblies.");
        sb.AppendLine("    /// This is a convenience method that calls AddEventHandlers() for each assembly.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddAllEventHandlers(this IServiceCollection services)");
        sb.AppendLine("    {");

        // Call local registration if exists
        if (hasLocalHandlers)
        {
            sb.AppendLine($"        services.AddEventHandlers(); // Local handlers");
        }

        // Call each referenced registration
        foreach (var ns in referencedNamespaces.OrderBy(n => n))
        {
            sb.AppendLine($"        {ns}.EventHandlerExtensions.AddEventHandlers(services); // From {ns}");
        }

        if (!hasLocalHandlers && referencedNamespaces.Count == 0)
        {
            sb.AppendLine("        // No event handlers discovered in this assembly or referenced assemblies");
        }

        sb.AppendLine();
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateRegistrationExtensions(List<EventHandlerInfo> handlers, string targetNamespace)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine("using Microsoft.Extensions.Hosting;");
        sb.AppendLine("using Zonit.Messaging.Events;");
        sb.AppendLine("using Zonit.Messaging.Events.Hosting;");
        sb.AppendLine();

        // Dodaj using dla ka¿dego namespace handlera (tylko jeœli ró¿ny od target namespace)
        var namespaces = handlers.Select(h => h.Namespace).Distinct().OrderBy(n => n);
        foreach (var ns in namespaces)
        {
            if (ns != "Zonit.Messaging.Events" && ns != "Global" && ns != targetNamespace)
            {
                sb.AppendLine($"using {ns};");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {targetNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated extension methods for registering event handlers.");
        sb.AppendLine("/// This class is generated by EventHandlerGenerator.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class EventHandlerExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all discovered event handlers (IEventHandler&lt;T&gt;) in the DI container.");
        sb.AppendLine("    /// This method is AOT/Trimming safe - no reflection at runtime.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddEventHandlers(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Register event provider services");
        sb.AppendLine("        services.TryAddSingleton<IEventManager, EventManager>();");
        sb.AppendLine("        services.TryAddSingleton<IEventProvider, EventProvider>();");
        sb.AppendLine("        services.AddHostedService<EventHandlerRegistrationHostedService>();");
        sb.AppendLine();

        foreach (var handler in handlers)
        {
            sb.AppendLine($"        // Handler: {handler.HandlerName} for event: {handler.EventName}");
            sb.AppendLine($"        services.AddScoped<{handler.HandlerFullName}>();");
            sb.AppendLine($"        services.AddScoped<IEventHandler<{handler.EventFullName}>>(sp => sp.GetRequiredService<{handler.HandlerFullName}>());");
            sb.AppendLine($"        services.AddSingleton<EventHandlerRegistration>(new EventHandlerRegistration<{handler.EventFullName}>());");
            sb.AppendLine();
        }

        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the list of all discovered event handler types for AOT-safe subscription.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static System.Type[] GetEventHandlerTypes()");
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

    private record EventHandlerInfo(
        string HandlerFullName,
        string HandlerName,
        string EventFullName,
        string EventName,
        string Namespace
    );
}
