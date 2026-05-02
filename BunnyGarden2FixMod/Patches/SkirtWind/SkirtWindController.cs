using System.Collections.Generic;
using System.Linq;
using GB;
using GB.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BunnyGarden2FixMod.Patches.SkirtWind;

public class SkirtWindController : MonoBehaviour
{
    public static SkirtWindController Instance { get; private set; }

    private const float MaxWeight = 5f;

    private class EntryData(float value = 0f)
    {
        public bool Enabled = true;
        public float Value = value;
    }

    private SkirtWindView m_view;

    private readonly EntryData m_allWeight = new();
    private readonly Dictionary<CharID, EntryData> m_skirtWeights = [];
    private readonly EntryData m_globalWeight = new(1f);

    private float m_animSpeed = 1f;

    public static void Initialize(GameObject parent)
    {
        parent.AddComponent<SkirtWindController>();
    }

    public float GetWeight(CharID charId)
    {
        if (!m_globalWeight.Enabled || m_globalWeight.Value <= 0f)
            return 0f;
        if (!m_skirtWeights.TryGetValue(charId, out var weight) || !weight.Enabled || weight.Value <= 0f)
            return 0f;
        return weight.Value * m_globalWeight.Value;
    }
    
    public float GetAnimSpeed() => m_animSpeed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Plugin.Logger.LogWarning(
                $"[SkirtWind] 複数の{nameof(SkirtWindController)}が検出されました。新しいものを破棄しています。");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        m_view = gameObject.AddComponent<SkirtWindView>();
        m_view.OnCloseClicked += HandleCloseClicked;
        m_view.OnEntryEnabledChanged += HandleEntryEnabledChanged;
        m_view.OnEntryValueChanged += HandleEntryValueChanged;
        m_view.OnAnimSpeedChanged += HandleAnimSpeedChanged;
    }

    private void OnDestroy()
    {
        if (m_view != null)
        {
            m_view.OnCloseClicked -= HandleCloseClicked;
            m_view.OnEntryEnabledChanged -= HandleEntryEnabledChanged;
            m_view.OnEntryValueChanged -= HandleEntryValueChanged;
            m_view.OnAnimSpeedChanged -= HandleAnimSpeedChanged;
        }

        if (Instance == this)
            Instance = null;
    }

    private void HandleCloseClicked()
    {
        m_view.Hide();
    }

    private void HandleEntryEnabledChanged(SkirtWindView.EntryKey key, bool enabled)
    {
        switch (key)
        {
            case SkirtWindView.EntryKey.All:
                m_allWeight.Enabled = enabled;
                foreach (var weight in m_skirtWeights.Values)
                    weight.Enabled = enabled;
                break;

            case SkirtWindView.EntryKey.Char charKey:
                if (m_skirtWeights.TryGetValue(charKey.CharId, out var skirtWeight))
                    skirtWeight.Enabled = enabled;
                if (m_skirtWeights.Values.All(w => w.Enabled == enabled))
                    m_allWeight.Enabled = enabled;
                break;

            case SkirtWindView.EntryKey.Global:
                m_globalWeight.Enabled = enabled;
                break;
        }

        m_view.Render(BuildRenderData());
    }

    private void HandleEntryValueChanged(SkirtWindView.EntryKey key, float value, bool committed)
    {
        switch (key)
        {
            case SkirtWindView.EntryKey.All:
                m_allWeight.Value = value;
                break;

            case SkirtWindView.EntryKey.Char charKey:
                if (m_skirtWeights.TryGetValue(charKey.CharId, out var skirtWeight))
                    skirtWeight.Value = value;
                break;

            case SkirtWindView.EntryKey.Global:
                m_globalWeight.Value = value;
                break;
        }

        if (committed)
            m_view.Render(BuildRenderData());
    }

    private void HandleAnimSpeedChanged(float value, bool committed)
    {
        m_animSpeed = value;
        if (committed)
            m_view.Render(BuildRenderData());
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return;

        if (Configs.SkirtWindShow?.IsTriggered() == true)
        {
            if (m_view.IsShown)
            {
                m_view.Hide();
            }
            else if (CanOpen())
            {
                Open();
            }
        }

        if (!m_view.IsShown) return;

        // パネル上でのキーボード操作
        if (!m_view.IsPointerOverPanel()) return;

        // Esc: 閉じる
        if (kb[Key.Escape].wasPressedThisFrame)
        {
            m_view.Hide();
            return;
        }
    }

    private bool CanOpen()
    {
        var system = GBSystem.Instance;
        return system != null && system.GetActiveEnvScene() != null;
    }

    private void Open()
    {
        m_view.Show(BuildRenderData());
    }

    private void MoveSelection(int delta)
    {
        m_view.Render(BuildRenderData());
    }
    
    private static EntryData DefaultEntryData() => new()
    {
        Enabled = true,
        Value = 0f,
    };

    private SkirtWindView.RenderData BuildRenderData()
    {
        static SkirtWindView.EntryData create(EntryData data, SkirtWindView.EntryKey key, float? maxValue = null)
            => new()
            {
                Key = key,
                Enabled = data.Enabled,
                Value = data.Value,
                MaxValue = maxValue ?? MaxWeight,
            };

        return new SkirtWindView.RenderData
        {
            All = create(m_allWeight, new SkirtWindView.EntryKey.All()),
            Characters = CharIDUtil.AllCharID().ConvertAll(charId =>
            {
                if (!m_skirtWeights.TryGetValue(charId, out var weight))
                    m_skirtWeights[charId] = weight = new EntryData();
                return create(weight, new SkirtWindView.EntryKey.Char(charId));
            }),
            Global = create(m_globalWeight, new SkirtWindView.EntryKey.Global(), 1f),
            AnimSpeed = m_animSpeed,
        };
    }
}