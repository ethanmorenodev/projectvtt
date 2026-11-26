using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Godot;
using ProjectVTT.Desktop.Api;
using ProjectVTT.Desktop.Ui;

namespace ProjectVTT.Desktop;

/// <summary>
/// Recorrido de humo end-to-end (`--smoke`) contra el backend real, con
/// las mismas clases que usa la UI: registro, campaña, portada (404 y PNG
/// real), escena, token, conexión al hub, `SyncScene`, `MoveToken` y evento
/// `TokenMoved`. Añade biblioteca (GET + filtros), actor, anotaciones
/// (evento `SceneAnnotationsUpdated` + GET) y `GetImageFileAsync`. Al final
/// monta las vistas reales para comprobar que construyen y cargan.
/// Sale con código 0 si todo cuadra; 1 con el error; 2 por tiempo de
/// espera. Se ejecuta con el servidor en marcha (`ProjectVTT-inicio`).
/// </summary>
public static class Smoke
{
    public static async Task RunAsync(
        Action<int> quit,
        Func<ApiClient, CampaignSummary, SceneDetail, SceneConnection, Task> mountViews)
    {
        var flow = ExecuteAsync(mountViews);
        var winner = await Task.WhenAny(flow, Task.Delay(TimeSpan.FromSeconds(60)));
        if (winner != flow)
        {
            GD.PrintErr("SMOKE FAIL: tiempo de espera agotado (¿está el backend en marcha?).");
            quit(2);
            return;
        }

        try
        {
            await flow;
            quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"SMOKE FAIL: tipo={exception.GetType().FullName}");
            GD.PrintErr($"SMOKE FAIL: mensaje={exception.Message}");
            GD.PrintErr($"SMOKE FAIL: traza={exception.StackTrace ?? "(sin traza)"}");
            GD.PrintErr($"SMOKE FAIL: inner={exception.InnerException?.ToString() ?? "(sin inner)"}");
            quit(1);
        }
    }

    /// <summary>Sube una portada PNG mínima con el contrato multipart existente.</summary>
    private static async Task UploadCoverAsync(ApiClient api, Guid campaignId)
    {
        var image = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        image.Fill(DesktopTheme.Amber);
        var png = image.SavePngToBuffer();

        using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(api.BaseUrl + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.Token);
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(png);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(part, "file", "cover.png");
        using var response = await http.PutAsync($"api/v1/campaigns/{campaignId}/cover", form);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"No se pudo subir la portada: {(int)response.StatusCode}.");
        }
    }

    /// <summary>Prepara una anotación real con el contrato de ADR-043.</summary>
    private static async Task PutAnnotationAsync(ApiClient api, Guid sceneId)
    {
        using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(api.BaseUrl + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.Token);
        var body = new
        {
            commandId = Guid.NewGuid(),
            expectedVersion = 0L,
            annotationId = (Guid?)null,
            kind = "note",
            points = Array.Empty<object>(),
            text = "Nota de humo",
            clear = false,
        };
        using var response = await http.PutAsync(
            $"api/v1/scenes/{sceneId}/annotations",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"No se pudo crear la anotación: {(int)response.StatusCode}.");
        }
    }

    /// <summary>Crea un actor válido para dnd5e con el contrato HTTP existente.</summary>
    private static async Task<ActorDto> CreateActorAsync(ApiClient api, Guid campaignId)
    {
        using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(api.BaseUrl + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.Token);
        var body = new
        {
            name = "Héroe de humo",
            rulesetKey = "dnd5e",
            attributes = new Dictionary<string, int>
            {
                ["strength"] = 10, ["dexterity"] = 10, ["constitution"] = 10,
                ["intelligence"] = 10, ["wisdom"] = 10, ["charisma"] = 10,
            },
            resources = new { hitPoints = new { current = 10, max = 10 } },
            kind = "pc",
            level = 1,
            biography = "Personaje de prueba del smoke.",
        };
        using var response = await http.PostAsync(
            $"api/v1/campaigns/{campaignId}/actors",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"No se pudo crear el actor: {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return (await JsonSerializer.DeserializeAsync<ActorDto>(stream, options))!;
    }

    private static async Task ExecuteAsync(
        Func<ApiClient, CampaignSummary, SceneDetail, SceneConnection, Task> mountViews)
    {
        using var api = new ApiClient();

        // Usuario nuevo por ejecución: no pisa datos de nadie.
        var userName = $"smoke_{Guid.NewGuid():N}"[..18];
        var auth = await api.RegisterAsync(userName, "smoke-1234");
        api.Token = auth.Token;
        GD.Print($"SMOKE: registro ok ({userName}) → {api.BaseUrl}");

        var campaign = await api.CreateCampaignAsync($"Smoke {DateTime.UtcNow:HHmmss}");
        GD.Print($"SMOKE: campaña ok ({campaign.Id})");

        // Portada: primero el 404 (la lista cae a la inicial) y después un
        // PNG real subido con el contrato existente, descargado y decodificado.
        var emptyCover = await api.GetCampaignCoverAsync(campaign.Id);
        if (emptyCover is not null)
        {
            throw new InvalidOperationException("Una campaña nueva no debería tener portada.");
        }

        GD.Print("SMOKE: portada ausente → inicial");
        await UploadCoverAsync(api, campaign.Id);
        var cover = await api.GetCampaignCoverAsync(campaign.Id);
        if (cover is null || !ImageBytes.TryDecode(cover.Value.Bytes, out var coverImage))
        {
            throw new InvalidOperationException("La portada subida no se pudo descargar/decodificar.");
        }

        GD.Print($"SMOKE: portada descargada y decodificada ({coverImage.GetWidth()}x{coverImage.GetHeight()})");

        // Rebanada 2: biblioteca de solo lectura y sus filtros opcionales.
        var types = await api.ListContentTypesAsync();
        var items = await api.ListContentItemsAsync();
        var filtered = await api.ListContentItemsAsync(
            system: "__smoke_no_system__", typeKey: "__smoke_no_type__");
        if (filtered.Count != 0)
        {
            throw new InvalidOperationException(
                $"Los filtros de biblioteca devolvieron {filtered.Count} ítems; se esperaba 0.");
        }

        GD.Print($"SMOKE: biblioteca ok ({types.Count} tipos, {items.Count} ítems, filtros aplicados)");

        var actor = await CreateActorAsync(api, campaign.Id);
        var actors = await api.ListActorsAsync(campaign.Id);
        if (actors.Count == 0 || actors.All(candidate => candidate.Id != actor.Id))
        {
            throw new InvalidOperationException("ListActors no devolvió el actor creado.");
        }

        GD.Print($"SMOKE: actores ok ({actors.Count}); retrato " +
            (actor.PortraitId is null ? "ausente → inicial" : "presente"));

        if (await api.GetImageFileAsync(Guid.NewGuid()) is not null)
        {
            throw new InvalidOperationException("Un imageId inexistente debería dar null (404).");
        }

        GD.Print("SMOKE: GetImageFileAsync devuelve null ante un 404");

        var scene = await api.CreateSceneAsync(campaign.Id, "Escena");
        GD.Print($"SMOKE: escena ok ({scene.Id}, grid {scene.GridShape} {scene.GridCellSize}px)");

        var token = await api.CreateTokenAsync(scene.Id, "Token 1", 128, 128);
        GD.Print($"SMOKE: token ok ({token.Id}, versión {token.Version})");

        await using var connection = new SceneConnection(api.BaseUrl, () => api.Token);
        var moved = new TaskCompletionSource<TokenMovedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.TokenMoved += e => moved.TrySetResult(e);
        var annotationsUpdated = new TaskCompletionSource<SceneAnnotationsUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SceneAnnotationsUpdated += e => annotationsUpdated.TrySetResult(e);
        await connection.StartAsync();
        await connection.JoinCampaignAsync(campaign.Id);
        GD.Print("SMOKE: hub conectado y unido a la campaña");

        // Rebanada 3: el hub entrega el estado completo tras persistir.
        await PutAnnotationAsync(api, scene.Id);
        var annotationsWinner = await Task.WhenAny(
            annotationsUpdated.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        if (annotationsWinner != annotationsUpdated.Task)
        {
            throw new InvalidOperationException(
                "No llegó el evento SceneAnnotationsUpdated tras crear una anotación.");
        }

        if (annotationsUpdated.Task.Result.Annotations.SceneId != scene.Id)
        {
            throw new InvalidOperationException("SceneAnnotationsUpdated llegó de otra escena.");
        }

        var annotations = await api.GetSceneAnnotationsAsync(scene.Id);
        if (annotations.Items.Count != 1 || annotations.Items[0].Text != "Nota de humo")
        {
            throw new InvalidOperationException(
                $"GET annotations devolvió {annotations.Items.Count} items; se esperaba 1.");
        }

        GD.Print($"SMOKE: anotaciones ok (evento + GET, v{annotations.Version})");

        var state = await connection.SyncSceneAsync(scene.Id);
        if (state.Tokens.Count != 1 || state.Tokens[0].Id != token.Id)
        {
            throw new InvalidOperationException(
                $"SyncScene devolvió {state.Tokens.Count} tokens; se esperaba 1.");
        }

        var result = await connection.MoveTokenAsync(new MoveTokenCommand(
            Guid.NewGuid(), scene.Id, token.Id, token.Version, 256, 256));
        if (Math.Abs(result.X - 256) > 0.001 || result.Duplicate)
        {
            throw new InvalidOperationException(
                $"MoveToken devolvió ({result.X}, {result.Y}) duplicate={result.Duplicate}.");
        }

        var winner = await Task.WhenAny(moved.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        if (winner != moved.Task)
        {
            throw new InvalidOperationException("No llegó el evento TokenMoved tras mover un token.");
        }

        if (Math.Abs(moved.Task.Result.X - 256) > 0.001)
        {
            throw new InvalidOperationException(
                $"TokenMoved llegó con X={moved.Task.Result.X}; se esperaba 256.");
        }

        // El mapa es opcional: solo comprobamos que la llamada responde.
        _ = await api.GetMapAsync(scene.Id);
        GD.Print("SMOKE: movimiento en vivo verificado (MoveToken + TokenMoved + SyncScene)");

        // Montaje de las vistas reales (construcción + cargas async).
        await mountViews(api, campaign, scene, connection);
        GD.Print("SMOKE: vistas (campañas, escenas, escena, biblioteca y personajes) construidas y cargadas");
        GD.Print("SMOKE OK: login, campañas, escenas, mapa, tokens, movimiento en vivo, biblioteca y anotaciones funcionan.");
    }
}
