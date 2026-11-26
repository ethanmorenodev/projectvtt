using ProjectVTT.Desktop.Api;

namespace ProjectVTT.Desktop.State;

/// <summary>
/// Estado autoritativo de los tokens de una escena. Réplica de la
/// semántica web (`apps/web/src/realtime/tokenStore.ts`): cada
/// actualización se aplica solo si trae una versión más nueva, de modo que
/// eventos reordenados o duplicados no corrompen el estado y una
/// resincronización (`ApplySync`) siempre manda.
/// </summary>
public sealed class TokenView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public long Version { get; set; }
    public Guid? ActorId { get; set; }
}

public sealed class TokenStore
{
    private readonly Dictionary<Guid, TokenView> _tokens = new();

    public IReadOnlyCollection<TokenView> Tokens => _tokens.Values;

    public TokenView? Get(Guid id) => _tokens.TryGetValue(id, out var token) ? token : null;

    /// <summary>Sustituye todo el estado por el del servidor.</summary>
    public void ApplySync(IEnumerable<TokenDto> tokens)
    {
        _tokens.Clear();
        foreach (var token in tokens)
        {
            _tokens[token.Id] = ToView(token);
        }
    }

    /// <summary>Aplica `TokenMoved` solo si `AggregateVersion` es estrictamente mayor.</summary>
    public bool ApplyTokenMoved(TokenMovedEvent e)
    {
        var current = Get(e.TokenId);
        if (current is not null && current.Version >= e.AggregateVersion)
        {
            return false;
        }

        _tokens[e.TokenId] = new TokenView
        {
            Id = e.TokenId,
            Name = current?.Name ?? e.TokenId.ToString(),
            X = e.X,
            Y = e.Y,
            Version = e.AggregateVersion,
            ActorId = current?.ActorId,
        };
        return true;
    }

    /// <summary>Añade o actualiza un token si trae una versión igual o más nueva.</summary>
    public bool Upsert(TokenDto token)
    {
        var current = Get(token.Id);
        if (current is not null && current.Version > token.Version)
        {
            return false;
        }

        _tokens[token.Id] = ToView(token);
        return true;
    }

    /// <summary>Movimiento optimista local con la versión que se enviará al servidor.</summary>
    public void ApplyLocalMove(Guid tokenId, double x, double y, long expectedVersion)
    {
        var current = Get(tokenId);
        if (current is null)
        {
            return;
        }

        current.X = x;
        current.Y = y;
        current.Version = expectedVersion;
    }

    /// <summary>Confirma el resultado del servidor (versión y coordenadas reales).</summary>
    public void ApplyMoveResult(MoveTokenResult result)
    {
        var current = Get(result.TokenId);
        if (current is null)
        {
            return;
        }

        current.X = result.X;
        current.Y = result.Y;
        current.Version = result.Version;
    }

    private static TokenView ToView(TokenDto token) => new()
    {
        Id = token.Id,
        Name = token.Name,
        X = token.X,
        Y = token.Y,
        Version = token.Version,
        ActorId = token.ActorId,
    };
}
