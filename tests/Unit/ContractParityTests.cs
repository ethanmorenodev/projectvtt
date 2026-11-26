using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProjectVTT.Tests.Unit;

/// <summary>
/// Paridad entre los esquemas JSON autoritativos de <c>packages/contracts/</c>
/// y los DTOs C# escritos a mano (<c>backend/Api/Realtime/Contracts.cs</c>,
/// <c>backend/Api/Rules/Dtos.cs</c>, ...). Sin dependencias nuevas: solo
/// <see cref="System.Text.Json"/> y reflexión sobre el assembly de la API.
/// <para>
/// Casos documentados que se resuelven con la normalización, sin excluir
/// ningún esquema: <c>SceneAnnotationsUpdated</c> anida el estado en
/// <c>annotations</c> (propiedad <c>Annotations</c>); <c>SceneDeleted</c> no
/// declara <c>aggregateVersion</c> (solo se exigen las propiedades que el
/// esquema marca como requeridas); <c>MapUpdated</c> usa <c>map</c> en JSON y
/// <c>Map</c> en C# (camelCase). Los comandos <c>MoveToken</c> y
/// <c>SetSceneAnnotation</c> se emparejan con <c>MoveTokenCommand</c> y
/// <c>SetSceneAnnotationRequest</c>.
/// </para>
/// </summary>
public class ContractParityTests
{
    private static readonly string[] NameSuffixes = ["event", "command", "request", "dto"];

    /// <summary>
    /// Equivalencia explícita cuando el nombre corto del <c>$def</c> no coincide
    /// con el nombre del DTO (ni con su normalización). Las claves son la
    /// normalización del nombre del <c>$def</c>; los valores son los nombres
    /// reales de los tipos C# y TypeScript, con la justificación del empareje.
    /// Si un <c>$def</c> nuevo no aparece aquí y no resuelve por normalización,
    /// el test falla: nunca se omite en silencio.
    /// </summary>
    private sealed record DefEquivalent(string CSharpType, string TypeScriptInterface, string Justification)
    {
        public string CSharpKey => Normalize(CSharpType);
        public string TypeScriptKey => Normalize(TypeScriptInterface);
    }

    private static readonly Dictionary<string, DefEquivalent> DefEquivalents = new(StringComparer.Ordinal)
    {
        ["actor"] = new("ActorDto", "ActorDto",
            "El $def es el Actor serializado; NO la entidad de dominio ProjectVTT.Api.Domain.Actor."),
        ["combatant"] = new("CombatantDto", "CombatantDto",
            "El $def es el combatiente del evento; NO la entidad de dominio ProjectVTT.Api.Domain.Combatant."),
        ["roll"] = new("RollDto", "RollDto",
            "El $def es la tirada serializada; NO la entidad de dominio ProjectVTT.Api.Domain.Roll."),
        ["annotations"] = new("SceneAnnotationsStateDto", "SceneAnnotationsState",
            "El $def describe el estado completo; el DTO/interface se llama SceneAnnotationsState(Dto)."),
        ["annotation"] = new("SceneAnnotationDto", "SceneAnnotationDto",
            "El $def usa el singular del dominio; el DTO añade el prefijo Scene."),
        ["point"] = new("SceneAnnotationPointDto", "SceneAnnotationPointDto",
            "Punto de una anotación de escena; el tipo comparte el prefijo SceneAnnotation."),
        ["cell"] = new("FogCellDto", "FogCellDto",
            "Celda de niebla revelada: tiene DTO propio (par x,y), no se trata como tupla sin tipo."),
        ["fog"] = new("FogStateDto", "FogStateDto",
            "El $def se llama 'fog' pero transporta el estado completo de la niebla."),
        ["map"] = new("MapAssetDto", "MapAssetDto",
            "Metadatos del mapa persistido; el DTO/description usan el sufijo Asset."),
        ["resource"] = new("ResourceValue", "ResourceValue",
            "Par {current,max} de un recurso; el tipo se llama ResourceValue."),
        ["breakdown"] = new("RollBreakdown", "RollBreakdown",
            "Desglose de la tirada; el tipo comparte el prefijo Roll."),
        ["appliedeffect"] = new("AppliedEffect", "RollEffect",
            "Efecto aplicado: C# lo llama AppliedEffect y TS RollEffect (mismo shape name/kind/value)."),
        ["item"] = new("ActorItemDto", "ActorItemDto",
            "Ítem del inventario de un Actor; el DTO añade el prefijo Actor."),
        ["effect"] = new("ActorEffectDto", "ActorEffectDto",
            "Efecto/condición de un Actor; el DTO añade el prefijo Actor."),
        ["combat"] = new("CombatStateDto", "CombatStateDto",
            "El $def transporta el estado completo del combate, de ahí el sufijo State."),
    };

    /// <summary>
    /// Emparejes que dependen del esquema padre: dos eventos distintos usan un
    /// <c>$def</c> con el mismo nombre 'message' pero shapes incompatibles.
    /// La clave es "claveNormalizadaDelEsquema|claveNormalizadaDelDef".
    /// </summary>
    private static readonly Dictionary<string, DefEquivalent> ContextualDefEquivalents = new(StringComparer.Ordinal)
    {
        ["messageposted|message"] = new("ChatMessageDto", "ChatMessageDto",
            "MessagePosted publica un mensaje del chat de campaña."),
        ["directmessageposted|message"] = new("DirectMessageDto", "DirectMessageDto",
            "DirectMessagePosted publica un mensaje privado 1:1 (ADR-017)."),
    };

    /// <summary>
    /// Excepciones explícitas: <c>$def</c> que son una tupla/celda sin DTO propio;
    /// para ellas solo se comprueba que siguen declarando <c>required</c> no vacío.
    /// Hoy está vacía: 'cell' (niebla) y 'point' (anotaciones) sí tienen DTO en
    /// C# y en TypeScript, así que se validan como cualquier otro <c>$def</c>.
    /// Añadir un nombre aquí sin justificación es un cambio de contrato a coordinar.
    /// </summary>
    private static readonly HashSet<string> DefsWithoutDto = new(StringComparer.Ordinal);

    private sealed record TypeScriptInterface(
        string Name,
        HashSet<string> RequiredMembers,
        HashSet<string> AllMembers);

    private static readonly Regex TypeScriptInterfaceHeader =
        new(@"export\s+interface\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.Compiled);

    private static readonly Regex TypeScriptMember =
        new(@"^\s*([A-Za-z_][A-Za-z0-9_]*)(\?)?\s*:", RegexOptions.Compiled);

    /// <summary>
    /// Localiza la raíz del repositorio subiendo desde el directorio de salida
    /// del test (que puede estar bajo <c>.artifacts-parity/</c>).
    /// </summary>
    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 12 && directory is not null; depth++, directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PROJECT_SPEC.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"No se encontró PROJECT_SPEC.md subiendo desde '{AppContext.BaseDirectory}'. " +
            "El test de paridad necesita la raíz del repositorio para localizar packages/contracts/.");
    }

    private static IReadOnlyList<string> DiscoverSchemas()
    {
        var contracts = Path.Combine(FindRepoRoot(), "packages", "contracts");
        var directories = new[] { "events", "commands" }
            .Select(name => Path.Combine(contracts, name))
            .Where(Directory.Exists);

        return directories
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.schema.json", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Clave de comparación: sin sufijos (Event/Command/Request/Dto), sin
    /// guiones/espacios/puntuación, sin versión (<c>v1</c>) y en minúsculas.
    /// Se usa tanto para el <c>title</c> del esquema como para el nombre del
    /// tipo C#; por eso los casos especiales pasan por la misma regla.
    /// </summary>
    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // "TokenMoved (evento)" -> "TokenMoved".
        var parenthesis = value.IndexOf('(');
        if (parenthesis >= 0)
        {
            value = value[..parenthesis];
        }

        var key = new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        key = StripTrailingVersion(key);

        foreach (var suffix in NameSuffixes)
        {
            if (key.EndsWith(suffix, StringComparison.Ordinal))
            {
                return key[..^suffix.Length];
            }
        }

        return key;
    }

    private static string StripTrailingVersion(string key)
    {
        for (var index = key.Length - 1; index >= 0 && char.IsDigit(key[index]); index--)
        {
            if (index > 0 && key[index - 1] == 'v')
            {
                return key[..(index - 1)];
            }
        }

        return key;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string NameFromId(string id)
    {
        var slash = id.LastIndexOf('/');
        return slash >= 0 ? id[(slash + 1)..] : id;
    }

    private static string NameFromFileName(string file) =>
        Path.GetFileName(file).Replace(".schema.json", string.Empty, StringComparison.Ordinal);

    private static Dictionary<string, List<Type>> BuildCSharpTypes()
    {
        var typesByKey = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
        foreach (var type in typeof(global::Program).Assembly.GetExportedTypes())
        {
            var key = Normalize(type.Name);
            if (key.Length == 0)
            {
                continue;
            }

            if (!typesByKey.TryGetValue(key, out var candidates))
            {
                candidates = [];
                typesByKey[key] = candidates;
            }

            candidates.Add(type);
        }

        return typesByKey;
    }

    /// <summary>
    /// Parseo mínimo de <c>apps/web/src/api/types.ts</c> sin dependencias:
    /// encuentra cada <c>export interface X { ... }</c> con balanceo de llaves
    /// y extrae sus miembros de primer nivel (<c>nombre:</c> / <c>nombre?:</c>).
    /// Los miembros opcionales se guardan aparte para no confundirlos con los
    /// requeridos por el esquema.
    /// </summary>
    private static Dictionary<string, List<TypeScriptInterface>> ParseTypeScriptInterfaces(string path)
    {
        var text = File.ReadAllText(path);
        var byKey = new Dictionary<string, List<TypeScriptInterface>>(StringComparer.Ordinal);

        foreach (Match match in TypeScriptInterfaceHeader.Matches(text))
        {
            var name = match.Groups[1].Value;
            var start = match.Index + match.Length;
            var depth = 1;
            var end = start;
            for (; end < text.Length && depth > 0; end++)
            {
                if (text[end] == '{')
                {
                    depth++;
                }
                else if (text[end] == '}')
                {
                    depth--;
                }
            }

            if (depth != 0)
            {
                continue;
            }

            var required = new HashSet<string>(StringComparer.Ordinal);
            var all = new HashSet<string>(StringComparer.Ordinal);
            var memberDepth = 0;
            foreach (var line in text[start..(end - 1)].Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (memberDepth == 0)
                {
                    var member = TypeScriptMember.Match(line);
                    if (member.Success)
                    {
                        var memberName = member.Groups[1].Value;
                        all.Add(memberName);
                        if (!member.Groups[2].Success)
                        {
                            required.Add(memberName);
                        }
                    }
                }

                memberDepth += line.Count(character => character == '{') - line.Count(character => character == '}');
            }

            var key = Normalize(name);
            if (key.Length == 0)
            {
                continue;
            }

            if (!byKey.TryGetValue(key, out var interfaces))
            {
                interfaces = [];
                byKey[key] = interfaces;
            }

            interfaces.Add(new TypeScriptInterface(name, required, all));
        }

        return byKey;
    }

    private static List<string> ReadRequired(JsonElement element, string label, List<string> problems)
    {
        if (!element.TryGetProperty("required", out var required) || required.ValueKind != JsonValueKind.Array)
        {
            problems.Add($"{label}: falta el array 'required'; no se puede comprobar la paridad.");
            return [];
        }

        if (required.GetArrayLength() == 0)
        {
            problems.Add($"{label}: 'required' está vacío; un $def sin propiedades obligatorias no aporta paridad.");
            return [];
        }

        return required.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
            .Where(property => !string.IsNullOrWhiteSpace(property))
            .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property!))
            .ToList();
    }

    private static string SchemaKey(JsonElement root, string label) =>
        Normalize(ReadString(root, "title")) is { Length: > 0 } fromTitle
            ? fromTitle
            : Normalize(ReadString(root, "$id")) is { Length: > 0 } fromId
                ? fromId
                : Normalize(Path.GetFileNameWithoutExtension(label));

    private static DefEquivalent? ResolveDefEquivalent(string schemaKey, string defKey)
    {
        if (ContextualDefEquivalents.TryGetValue($"{schemaKey}|{defKey}", out var contextual))
        {
            return contextual;
        }

        return DefEquivalents.TryGetValue(defKey, out var shared) ? shared : null;
    }

    private static void CheckCSharpType(
        IReadOnlyList<string> required,
        string key,
        string label,
        IReadOnlyDictionary<string, List<Type>> typesByKey,
        List<string> problems,
        string? preferredTypeName = null)
    {
        if (!typesByKey.TryGetValue(key, out var candidates) || candidates.Count == 0)
        {
            problems.Add($"{label}: sin tipo C# equivalente (clave '{key}'). " +
                "Declara la correspondencia en DefEquivalents/ContextualDefEquivalents con justificación o añade el tipo.");
            return;
        }

        // La entidad de dominio y su DTO normalizan a la misma clave. El alias
        // explícito designa el DTO del contrato; sin alias, la ambigüedad falla.
        if (preferredTypeName is not null)
        {
            candidates = candidates.Where(type => type.Name == preferredTypeName).ToList();
            if (candidates.Count == 0)
            {
                problems.Add($"{label}: el tipo C# declarado '{preferredTypeName}' no existe (clave '{key}').");
                return;
            }
        }

        if (candidates.Count > 1)
        {
            problems.Add($"{label}: tipos C# ambiguos para la clave '{key}': " +
                string.Join(", ", candidates.Select(type => type.FullName)) + ".");
            return;
        }

        var properties = candidates[0].GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
            .ToHashSet(StringComparer.Ordinal);
        var missing = required.Where(property => !properties.Contains(property)).ToList();
        if (missing.Count > 0)
        {
            problems.Add($"{label}: {candidates[0].FullName} no expone las propiedades requeridas: " +
                string.Join(", ", missing) + ".");
        }
    }

    private static void CheckTypeScriptType(
        IReadOnlyList<string> required,
        string key,
        string label,
        IReadOnlyDictionary<string, List<TypeScriptInterface>> interfacesByKey,
        List<string> problems)
    {
        if (!interfacesByKey.TryGetValue(key, out var candidates) || candidates.Count == 0)
        {
            problems.Add($"{label}: sin interfaz TypeScript equivalente (clave '{key}') en apps/web/src/api/types.ts. " +
                "Declara la correspondencia en DefEquivalents/ContextualDefEquivalents con justificación o añade la interfaz.");
            return;
        }

        if (candidates.Count > 1)
        {
            problems.Add($"{label}: interfaces TypeScript ambiguas para la clave '{key}': " +
                string.Join(", ", candidates.Select(candidate => candidate.Name)) + ".");
            return;
        }

        var found = candidates[0];
        var missing = required
            .Where(property => !found.RequiredMembers.Contains(property))
            .ToList();
        if (missing.Count > 0)
        {
            var detail = missing.Select(property =>
                found.AllMembers.Contains(property)
                    ? $"{property} (declarada opcional como '{property}?')"
                    : property);
            problems.Add($"{label}: la interfaz TS {found.Name} no declara como requeridas: " +
                string.Join(", ", detail) + ".");
        }
    }

    /// <summary>
    /// Núcleo común: raíz y <c>$defs</c> anidados contra C# y TypeScript. Se
    /// usa tanto con los esquemas del repositorio como con esquemas inyectados
    /// en memoria por los tests negativos.
    /// </summary>
    private static List<string> VerifySchema(
        JsonElement root,
        string label,
        IReadOnlyDictionary<string, List<Type>> typesByKey,
        IReadOnlyDictionary<string, List<TypeScriptInterface>> interfacesByKey)
    {
        var problems = new List<string>();
        var schemaKey = SchemaKey(root, label);

        var rootRequired = ReadRequired(root, label, problems);
        CheckCSharpType(rootRequired, schemaKey, label, typesByKey, problems);
        CheckTypeScriptType(rootRequired, schemaKey, label, interfacesByKey, problems);

        if (!root.TryGetProperty("$defs", out var defs) || defs.ValueKind != JsonValueKind.Object)
        {
            return problems;
        }

        foreach (var def in defs.EnumerateObject())
        {
            var defLabel = $"{label}/$defs/{def.Name}";
            var required = ReadRequired(def.Value, defLabel, problems);
            var defKey = Normalize(def.Name);

            if (DefsWithoutDto.Contains(defKey))
            {
                // Excepción explícita: solo se exige que el $def siga declarando
                // 'required' (ya comprobado por ReadRequired).
                continue;
            }

            var equivalent = ResolveDefEquivalent(schemaKey, defKey);
            CheckCSharpType(required, equivalent?.CSharpKey ?? defKey, defLabel, typesByKey, problems,
                equivalent?.CSharpType);
            CheckTypeScriptType(required, equivalent?.TypeScriptKey ?? defKey, defLabel, interfacesByKey, problems);
        }

        return problems;
    }

    /// <summary>
    /// Falla si alguien vacía la carpeta de contratos o deja un esquema sin
    /// <c>$id</c>, <c>title</c> o <c>required</c>.
    /// </summary>
    [Fact]
    public void SchemasAreDiscoverableAndDeclareRequiredProperties()
    {
        var schemas = DiscoverSchemas();
        Assert.True(schemas.Count > 0,
            "No se han encontrado *.schema.json en packages/contracts/events ni /commands. " +
            "La paridad no puede comprobarse si la carpeta se borra.");

        foreach (var file in schemas)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;

            Assert.False(string.IsNullOrWhiteSpace(ReadString(root, "$id")), $"{file}: falta $id.");
            Assert.False(string.IsNullOrWhiteSpace(ReadString(root, "title")), $"{file}: falta title.");
            Assert.True(
                root.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array,
                $"{file}: falta el array 'required'.");
            Assert.True(required.GetArrayLength() > 0, $"{file}: 'required' está vacío.");

            AssertNoEmptyRequired(root, file, "$");
        }
    }

    private static void AssertNoEmptyRequired(JsonElement element, string file, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var childPath = $"{path}.{property.Name}";
                if (property.NameEquals("required") &&
                    property.Value.ValueKind == JsonValueKind.Array &&
                    property.Value.GetArrayLength() == 0)
                {
                    Assert.Fail($"{file}: 'required' vacío en {childPath}.");
                }

                AssertNoEmptyRequired(property.Value, file, childPath);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                AssertNoEmptyRequired(item, file, $"{path}[{index++}]");
            }
        }
    }

    /// <summary>
    /// Comprueba, para todos los esquemas, que existe un tipo C# equivalente y
    /// que todas sus propiedades requeridas están presentes (normalizadas a
    /// camelCase). Los fallos se agrupan para reportar todos los esquemas
    /// sin equivalente de una sola vez.
    /// </summary>
    [Fact]
    public void EverySchemaHasEquivalentCSharpTypeCoveringRequiredProperties()
    {
        var schemas = DiscoverSchemas();
        Assert.True(schemas.Count > 0, "No se han encontrado esquemas para comprobar.");

        var typesByKey = BuildCSharpTypes();
        var problems = new List<string>();

        foreach (var file in schemas)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;
            var id = ReadString(root, "$id") ?? "(sin $id)";
            var title = ReadString(root, "title") ?? "(sin title)";
            var key = Normalize(title) is { Length: > 0 } name
                ? name
                : Normalize(NameFromId(id)) is { Length: > 0 } fromId
                    ? fromId
                    : Normalize(NameFromFileName(file));

            var required = root.GetProperty("required")
                .EnumerateArray()
                .Select(item => item.GetString())
                .Where(property => !string.IsNullOrWhiteSpace(property))
                .ToList();

            var label = $"{Path.GetFileName(file)} ($id='{id}', title='{title}')";

            if (!typesByKey.TryGetValue(key, out var candidates) || candidates.Count == 0)
            {
                problems.Add($"{label}: sin tipo C# equivalente (clave '{key}'). " +
                    "Decide con el coordinador si se añade el tipo o se excluye el esquema con justificación documentada.");
                continue;
            }

            if (candidates.Count > 1)
            {
                problems.Add($"{label}: tipos C# ambiguos para la clave '{key}': " +
                    string.Join(", ", candidates.Select(type => type.FullName)) + ".");
                continue;
            }

            var type = candidates[0];
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
                .ToHashSet(StringComparer.Ordinal);

            var missing = required.Where(property => !properties.Contains(property!)).ToList();
            if (missing.Count > 0)
            {
                problems.Add($"{label}: {type.FullName} no expone las propiedades requeridas: " +
                    string.Join(", ", missing) + ".");
            }
        }

        Assert.True(problems.Count == 0,
            "Paridad contratos JSON ↔ DTOs C# incumplida:" + Environment.NewLine +
            string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Extensión de la paridad: todos los esquemas (raíz y <c>$defs</c>) contra
    /// los DTOs C# y contra las interfaces TypeScript. Los fallos se agrupan
    /// para reportar de una vez todos los contratos desalineados.
    /// </summary>
    [Fact]
    public void EverySchemaRootAndNestedDefMatchesCSharpAndTypeScriptRequiredProperties()
    {
        var root = FindRepoRoot();
        var typesPath = Path.Combine(root, "apps", "web", "src", "api", "types.ts");
        Assert.True(File.Exists(typesPath), $"No se encontró '{typesPath}': la paridad TS no puede comprobarse.");

        var typesByKey = BuildCSharpTypes();
        var interfacesByKey = ParseTypeScriptInterfaces(typesPath);
        Assert.True(interfacesByKey.Count > 0, "No se han extraído interfaces de types.ts: el parser TS está roto.");

        var schemas = DiscoverSchemas();
        Assert.True(schemas.Count >= 16,
            $"Se esperaban los 16 esquemas de packages/contracts/events/*.schema.json; se encontraron {schemas.Count}. " +
            "Un esquema borrado no puede omitirse silenciosamente.");

        var problems = new List<string>();
        var defsChecked = 0;
        foreach (var file in schemas)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var label = Path.GetFileName(file);
            if (document.RootElement.TryGetProperty("$defs", out var defs) && defs.ValueKind == JsonValueKind.Object)
            {
                defsChecked += defs.EnumerateObject().Count();
            }

            problems.AddRange(VerifySchema(document.RootElement, label, typesByKey, interfacesByKey));
        }

        Assert.True(defsChecked > 0, "No se ha encontrado ningún $def anidado: la comprobación de $defs está vacía.");
        Assert.True(problems.Count == 0,
            "Paridad contratos JSON ↔ C# ↔ TypeScript incumplida:" + Environment.NewLine +
            string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Comprobación negativa: el verificador debe fallar si se añade un
    /// <c>required</c> inexistente (en la raíz o en un <c>$def</c>) o si se borra
    /// una interfaz TS. Sin estos casos el test podría pasar siempre.
    /// </summary>
    [Fact]
    public void ParityFailsForUnknownRequiredPropertyOrMissingTypeScriptInterface()
    {
        var typesPath = Path.Combine(FindRepoRoot(), "apps", "web", "src", "api", "types.ts");
        var typesByKey = BuildCSharpTypes();
        var interfacesByKey = ParseTypeScriptInterfaces(typesPath);
        Assert.True(interfacesByKey.Count > 0, "No se han extraído interfaces de types.ts.");

        const string injectedSchema = """
            {
              "$id": "projectvtt/events/map-updated.event.v1",
              "title": "MapUpdated (evento)",
              "required": ["eventId", "bogusRequiredPropertyParityProbe"],
              "$defs": {
                "point": {
                  "type": "object",
                  "required": ["x", "y", "bogusDefPropertyParityProbe"]
                }
              }
            }
            """;

        using (var document = JsonDocument.Parse(injectedSchema))
        {
            var problems = VerifySchema(document.RootElement, "esquema-falso-en-memoria", typesByKey, interfacesByKey);
            Assert.Contains(problems, problem => problem.Contains("bogusRequiredPropertyParityProbe", StringComparison.Ordinal));
            Assert.Contains(problems, problem => problem.Contains("bogusDefPropertyParityProbe", StringComparison.Ordinal));
        }

        const string mapSchema = """
            {
              "$id": "projectvtt/events/map-updated.event.v1",
              "title": "MapUpdated (evento)",
              "required": ["eventId", "map"]
            }
            """;

        var withoutMapUpdated = new Dictionary<string, List<TypeScriptInterface>>(interfacesByKey, StringComparer.Ordinal);
        Assert.True(withoutMapUpdated.Remove("mapupdated"), "No se encontró la interfaz TS MapUpdatedEvent para borrarla.");
        using (var document = JsonDocument.Parse(mapSchema))
        {
            var problems = VerifySchema(document.RootElement, "map-updated.schema.json", typesByKey, withoutMapUpdated);
            Assert.Contains(problems, problem => problem.Contains("sin interfaz TypeScript equivalente", StringComparison.Ordinal) &&
                problem.Contains("mapupdated", StringComparison.Ordinal));
        }
    }
}
