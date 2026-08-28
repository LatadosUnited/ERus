using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using ERus.Engine.Network.Packets.Events;

namespace ERus.Engine.Network.Collaboration;

public class CollaboratorInfo
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int SelectedNetworkId { get; set; } = -1;
    public byte ColorR { get; set; } = 255;
    public byte ColorG { get; set; } = 255;
    public byte ColorB { get; set; } = 255;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public Vector4 ColorVector => new Vector4(ColorR / 255f, ColorG / 255f, ColorB / 255f, 1.0f);
}

public class ChatMessageItem
{
    public int SenderId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public Vector4 Color { get; set; } = new Vector4(1, 1, 1, 1);
}

public class CollaboratorPresenceManager
{
    private readonly ConcurrentDictionary<int, CollaboratorInfo> _collaborators = new();
    private readonly List<ChatMessageItem> _chatHistory = new();
    private readonly List<string> _activityLog = new();
    private readonly object _chatLock = new();
    private readonly object _activityLock = new();

    public event Action? OnCollaboratorsChanged;
    public event Action<ChatMessageItem>? OnChatMessageReceived;
    public event Action<string>? OnActivityLogged;

    public IReadOnlyDictionary<int, CollaboratorInfo> Collaborators => _collaborators;
    public IReadOnlyList<ChatMessageItem> ChatHistory
    {
        get
        {
            lock (_chatLock) return _chatHistory.ToArray();
        }
    }
    public IReadOnlyList<string> ActivityLog
    {
        get
        {
            lock (_activityLock) return _activityLog.ToArray();
        }
    }

    public static (byte R, byte G, byte B) GenerateUserColor(string username)
    {
        if (string.IsNullOrEmpty(username)) return (120, 180, 255);
        int hash = System.Math.Abs(username.GetHashCode());
        float hue = (hash % 360) / 360f;
        return HsvToRgb(hue, 0.75f, 0.95f);
    }

    private static (byte R, byte G, byte B) HsvToRgb(float h, float s, float v)
    {
        int hi = (int)System.Math.Floor(h * 6) % 6;
        float f = h * 6 - (float)System.Math.Floor(h * 6);
        float p = v * (1 - s);
        float q = v * (1 - f * s);
        float t = v * (1 - (1 - f) * s);

        float r = 0, g = 0, b = 0;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            case 5: r = v; g = p; b = q; break;
        }

        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    public void UpdatePresence(UserPresencePacket packet)
    {
        if (packet.IsDisconnecting)
        {
            if (_collaborators.TryRemove(packet.UserId, out var removed))
            {
                LogActivity($"[Saída] {removed.Username} desconectou-se da sessão.");
                OnCollaboratorsChanged?.Invoke();
            }
            return;
        }

        bool isNew = !_collaborators.ContainsKey(packet.UserId);
        var info = _collaborators.GetOrAdd(packet.UserId, id => new CollaboratorInfo
        {
            UserId = packet.UserId,
            Username = packet.Username,
            ColorR = packet.ColorR,
            ColorG = packet.ColorG,
            ColorB = packet.ColorB
        });

        int prevSelection = info.SelectedNetworkId;
        info.Username = packet.Username;
        info.SelectedNetworkId = packet.SelectedNetworkId;
        info.ColorR = packet.ColorR;
        info.ColorG = packet.ColorG;
        info.ColorB = packet.ColorB;
        info.LastSeen = DateTime.UtcNow;

        if (isNew)
        {
            LogActivity($"[Conexão] {packet.Username} entrou na sessão de co-edição.");
        }
        else if (prevSelection != packet.SelectedNetworkId && packet.SelectedNetworkId != -1)
        {
            LogActivity($"[Seleção] {packet.Username} selecionou a entidade #{packet.SelectedNetworkId}.");
        }

        OnCollaboratorsChanged?.Invoke();
    }

    public void RemoveCollaborator(int userId)
    {
        if (_collaborators.TryRemove(userId, out var removed))
        {
            LogActivity($"[Saída] {removed.Username} desconectou-se.");
            OnCollaboratorsChanged?.Invoke();
        }
    }

    public CollaboratorInfo? GetCollaboratorSelecting(int networkId)
    {
        if (networkId == -1) return null;
        foreach (var c in _collaborators.Values)
        {
            if (c.SelectedNetworkId == networkId)
                return c;
        }
        return null;
    }

    public void AddChatMessage(ChatMessagePacket packet)
    {
        var item = new ChatMessageItem
        {
            SenderId = packet.SenderId,
            Username = packet.Username,
            Message = packet.Message,
            Timestamp = string.IsNullOrEmpty(packet.Timestamp) ? DateTime.Now.ToString("HH:mm:ss") : packet.Timestamp,
            Color = _collaborators.TryGetValue(packet.SenderId, out var c) ? c.ColorVector : new Vector4(0.8f, 0.8f, 0.8f, 1.0f)
        };

        lock (_chatLock)
        {
            _chatHistory.Add(item);
            if (_chatHistory.Count > 200) _chatHistory.RemoveAt(0);
        }

        OnChatMessageReceived?.Invoke(item);
    }

    public void LogActivity(string message)
    {
        string timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (_activityLock)
        {
            _activityLog.Add(timestamped);
            if (_activityLog.Count > 100) _activityLog.RemoveAt(0);
        }
        OnActivityLogged?.Invoke(timestamped);
    }

    public void Clear()
    {
        _collaborators.Clear();
        lock (_chatLock) _chatHistory.Clear();
        lock (_activityLock) _activityLog.Clear();
        OnCollaboratorsChanged?.Invoke();
    }
}
