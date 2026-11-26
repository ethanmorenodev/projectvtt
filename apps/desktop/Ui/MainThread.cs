using System.Collections.Concurrent;
using Godot;

namespace ProjectVTT.Desktop.Ui;

/// <summary>
/// Lleva trabajo ajeno al hilo principal de Godot (los eventos de SignalR
/// llegan en hilos del pool y tocar nodos desde ahí es inseguro). La cola
/// se vacía en cada frame desde `Main._Process`.
/// </summary>
public static class MainThread
{
    private static readonly ConcurrentQueue<Action> Queue = new();

    public static void Run(Action action) => Queue.Enqueue(action);

    public static void Drain()
    {
        while (Queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                GD.PushError($"Error en hilo principal: {exception}");
            }
        }
    }
}
