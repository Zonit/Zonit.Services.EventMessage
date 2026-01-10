using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace Zonit.Messaging.Commands.SourceGenerators;

/// <summary>
/// Source Generator który automatycznie generuje rejestracjê handlerów dla AOT/Trimming.
/// Skanuje projekt w poszukiwaniu klas implementuj¹cych IRequestHandler i generuje:
/// 1. Extension method AddCommandHandlers() dla automatycznej rejestracji wszystkich handlerów
/// 2. AOT-safe dispatch bez reflection
/// </summary>
[Generator]
public class CommandHandlerGenerator : IIncrementalGenerator
{
    private const string RequestHandlerInterfaceName = "IRequestHandler";
    private const string RequestHandlerNamespace = "Zonit.Messaging.Commands";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ZnajdŸ wszystkie klasy implementuj¹ce IRequestHandler<,>
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

    private static HandlerInfo? GetHandlerInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);

        if (symbol is not INamedTypeSymbol classSymbol)
            return null;

        // SprawdŸ czy klasa jest abstract lub static
        if (classSymbol.IsAbstract || classSymbol.IsStatic)
            return null;

        // ZnajdŸ interfejs IRequestHandler<TRequest, TResponse>
        var handlerInterface = classSymbol.AllInterfaces
            .FirstOrDefault(i => i.IsGenericType 
                && i.Name == RequestHandlerInterfaceName 
                && i.TypeArguments.Length == 2
                && i.ContainingNamespace?.ToDisplayString() == RequestHandlerNamespace);

        if (handlerInterface is null)
            return null;

        var requestType = handlerInterface.TypeArguments[0];
        var responseType = handlerInterface.TypeArguments[1];

        return new HandlerInfo(
            HandlerFullName: classSymbol.ToDisplayString(),
            HandlerName: classSymbol.Name,
            RequestFullName: requestType.ToDisplayString(),
            RequestName: requestType.Name,
            ResponseFullName: responseType.ToDisplayString(),
            ResponseName: responseType.Name,
            Namespace: classSymbol.ContainingNamespace?.ToDisplayString() ?? "Global"
        );
    }

    private static void Execute(Compilation compilation, ImmutableArray<HandlerInfo?> handlers, SourceProductionContext context)
    {
        var validHandlers = handlers
            .Where(h => h is not null)
            .Cast<HandlerInfo>()
            .Distinct()
            .ToList();

        // Get assembly name for namespace - use root namespace from assembly name
        var assemblyName = compilation.AssemblyName ?? "Unknown";
        var targetNamespace = assemblyName;
        var safeAssemblyName = assemblyName.Replace(".", "_").Replace("-", "_");

        // Scan referenced assemblies for CommandHandlerExtensions classes
        var referencedRegistrations = FindReferencedCommandRegistrations(compilation);

        // Generate local registration if we have handlers
        if (validHandlers.Count > 0)
        {
            var registrationSource = GenerateRegistrationExtensions(validHandlers, targetNamespace, safeAssemblyName);
            context.AddSource("CommandHandlerRegistration.g.cs", registrationSource);

            // Generuj AOT-safe CommandProvider
            var providerSource = GenerateAotCommandProvider(validHandlers, targetNamespace, safeAssemblyName);
            context.AddSource("GeneratedCommandProvider.g.cs", providerSource);
        }

        // Generate global registration only if there's something to register
        // (either local handlers or referenced registrations)
        if (validHandlers.Count > 0 || referencedRegistrations.Count > 0)
        {
            var globalSource = GenerateGlobalRegistration(targetNamespace, validHandlers.Count > 0, referencedRegistrations);
            context.AddSource("CommandHandlerGlobalRegistration.g.cs", globalSource);
        }
    }

    private static List<string> FindReferencedCommandRegistrations(Compilation compilation)
    {
        var registrations = new List<string>();

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assemblySymbol)
            {
                FindCommandHandlerExtensionsClasses(assemblySymbol.GlobalNamespace, registrations);
            }
        }

        return registrations.Distinct().ToList();
    }

    private static void FindCommandHandlerExtensionsClasses(INamespaceSymbol namespaceSymbol, List<string> registrations)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            if (type.Name == "CommandHandlerExtensions" && type.IsStatic)
            {
                var hasMethod = type.GetMembers("AddCommandHandlers")
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
            FindCommandHandlerExtensionsClasses(nestedNamespace, registrations);
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
        sb.AppendLine("/// Auto-generated global registration for all command handlers.");
        sb.AppendLine("/// Registers handlers from this assembly and all referenced assemblies.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class CommandHandlerGlobalExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all command handlers from this assembly and all referenced assemblies.");
        sb.AppendLine("    /// This is a convenience method that calls AddCommandHandlers() for each assembly.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddAllCommandHandlers(this IServiceCollection services)");
        sb.AppendLine("    {");

        if (hasLocalHandlers)
        {
            sb.AppendLine($"        services.AddCommandHandlers(); // Local handlers");
        }

        foreach (var ns in referencedNamespaces.OrderBy(n => n))
        {
            sb.AppendLine($"        {ns}.CommandHandlerExtensions.AddCommandHandlers(services); // From {ns}");
        }

        if (!hasLocalHandlers && referencedNamespaces.Count == 0)
        {
            sb.AppendLine("        // No command handlers discovered in this assembly or referenced assemblies");
        }

        sb.AppendLine();
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateRegistrationExtensions(List<HandlerInfo> handlers, string targetNamespace, string safeAssemblyName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine("using Zonit.Messaging.Commands;");
        sb.AppendLine();

        // Dodaj using dla ka¿dego namespace handlera (tylko jeœli ró¿ny od target namespace)
        var namespaces = handlers.Select(h => h.Namespace).Distinct().OrderBy(n => n);
        foreach (var ns in namespaces)
        {
            if (ns != "Zonit.Messaging.Commands" && ns != "Global" && ns != targetNamespace)
            {
                sb.AppendLine($"using {ns};");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {targetNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Auto-generated extension methods for registering command handlers.");
        sb.AppendLine("/// This class is generated by CommandHandlerGenerator.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class CommandHandlerExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all discovered command handlers in the DI container.");
        sb.AppendLine("    /// This method is AOT/Trimming safe - no reflection at runtime.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Add the command provider");
        sb.AppendLine($"        services.TryAddScoped<ICommandProvider, GeneratedCommandProvider_{safeAssemblyName}>();");
        sb.AppendLine();

        foreach (var handler in handlers)
        {
            sb.AppendLine($"        // Handler: {handler.HandlerName}");
            sb.AppendLine($"        services.AddScoped<IRequestHandler<{handler.RequestFullName}, {handler.ResponseFullName}>, {handler.HandlerFullName}>();");
        }

        sb.AppendLine();
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }


    private static string GenerateAotCommandProvider(List<HandlerInfo> handlers, string targetNamespace, string safeAssemblyName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Zonit.Messaging.Commands;");
        sb.AppendLine();
        sb.AppendLine($"namespace {targetNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// AOT-safe CommandProvider generated at compile time.");
        sb.AppendLine("/// Uses direct type resolution instead of MakeGenericType reflection.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"internal sealed class GeneratedCommandProvider_{safeAssemblyName} : ICommandProvider");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly IServiceProvider _serviceProvider;");
        sb.AppendLine();
        sb.AppendLine($"    public GeneratedCommandProvider_{safeAssemblyName}(IServiceProvider serviceProvider)");
        sb.AppendLine("    {");
        sb.AppendLine("        _serviceProvider = serviceProvider;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public Task<TResponse?> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : notnull");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(request);");
        sb.AppendLine();
        sb.AppendLine("        return request switch");
        sb.AppendLine("        {");

        foreach (var handler in handlers)
        {
            sb.AppendLine($"            {handler.RequestFullName} r => HandleAsync<{handler.RequestFullName}, {handler.ResponseFullName}, TResponse>(r, cancellationToken),");
        }

        sb.AppendLine("            _ => throw new InvalidOperationException(");
        sb.AppendLine("                $\"No handler registered for request type '{request.GetType().FullName}'. \" +");
        sb.AppendLine("                $\"Expected response type: '{typeof(TResponse).FullName}'. \" +");
        sb.AppendLine("                \"Ensure handler is registered using AddCommandHandlers() or AddCommand<THandler>().\")");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private Task<TResponse?> HandleAsync<TRequest, THandlerResponse, TResponse>(TRequest request, CancellationToken cancellationToken)");
        sb.AppendLine("        where TRequest : IRequest<THandlerResponse>");
        sb.AppendLine("        where THandlerResponse : notnull");
        sb.AppendLine("        where TResponse : notnull");
        sb.AppendLine("    {");
        sb.AppendLine("        var handler = _serviceProvider.GetRequiredService<IRequestHandler<TRequest, THandlerResponse>>();");
        sb.AppendLine("        ");
        sb.AppendLine("        // Safe cast - typy s¹ weryfikowane compile-time przez switch expression");
        sb.AppendLine("        return (Task<TResponse?>)(object)handler.HandleAsync(request, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private record HandlerInfo(
        string HandlerFullName,
        string HandlerName,
        string RequestFullName,
        string RequestName,
        string ResponseFullName,
        string ResponseName,
        string Namespace
    );
}
