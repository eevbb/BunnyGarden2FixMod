using System;
using System.Collections.Generic;
using GB.Game;
using UITKit;
using UITKit.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace BunnyGarden2FixMod.Patches.SkirtWind;

public class SkirtWindView : MonoBehaviour
{
    public abstract record EntryKey
    {
        public record All : EntryKey;
        public record Char(CharID CharId) : EntryKey { public CharID CharId = CharId; }
        public record Global : EntryKey;

        public string GetLabel() => this switch
        {
            All => "全員",
            Char c => $"{c.CharId}",
            Global => "全体",
            _ => "???",
        };
    }

    public class EntryData
    {
        public EntryKey Key;
        public bool Enabled;
        public float Value;
        public float MaxValue;
    }

    public class RenderData
    {
        public EntryData All;
        public IReadOnlyList<EntryData> Characters;
        public EntryData Global;
        public float AnimSpeed;
    }

    public event Action OnCloseClicked;
    public event Action<EntryKey, bool> OnEntryEnabledChanged;
    public event Action<EntryKey, float, bool> OnEntryValueChanged;
    public event Action<float, bool> OnAnimSpeedChanged;

    private UIDocument m_doc;
    private PanelSettings m_settings;
    private VisualElement m_root;
    private VisualElement m_panel;
    private VisualElement m_listContainer;
    private Font m_font;

    public bool IsShown => m_panel != null && m_panel.style.display != DisplayStyle.None;

    public bool IsPointerOverPanel()
    {
        if (!IsShown || m_panel == null) return false;
        if (m_root == null || m_root.panel == null) return false;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return false;
        var raw = mouse.position.ReadValue();
        var flipped = new Vector2(raw.x, Screen.height - raw.y);
        var panelPos = RuntimePanelUtils.ScreenToPanel(m_root.panel, flipped);
        return m_panel.worldBound.Contains(panelPos);
    }

    public void Show(RenderData data)
    {
        EnsureBuilt();
        if (m_settings != null) m_settings.scale = Configs.UIScale.Value;
        m_panel.style.display = DisplayStyle.Flex;
        Render(data);
    }

    public void Hide()
    {
        if (m_panel != null) m_panel.style.display = DisplayStyle.None;
    }

    public void Render(RenderData data)
    {
        if (m_panel == null || data == null) return;
        
        m_listContainer.Clear();

        HashSet<UITSlider> charSliders = [];
        m_listContainer.Add(BuildRow(data.All, charSliders));

        foreach (var charData in data.Characters)
            m_listContainer.Add(BuildRow(charData, charSliders));

        m_listContainer.Add(BuildRow(data.Global, charSliders));

        // AnimSpeed Slider
        {
            var row = BuildRow();
            row.Add(BuildLabel("モーション速度", 10, 24));
            var sl = new UITSlider();
            sl.Setup(0f, 1f, m_font, value => $"{value:0.00}");
            sl.SetValue(data.AnimSpeed);
            sl.SetNotchSize(0.1f);
            sl.SetStep(0.05f);
            sl.OnValueChanged += value => OnAnimSpeedChanged?.Invoke(value, false);
            sl.OnValueCommitted += value => OnAnimSpeedChanged?.Invoke(value, true);
            row.Add(sl);
            m_listContainer.Add(row);
        }
    }
    
    private VisualElement BuildRow()
    {
        var row = UITFactory.CreateRow();
        row.style.height = 28;
        row.style.marginTop = 1;
        row.style.marginBottom = 1;
        row.style.alignItems = Align.Center;
        return row;
    }
    
    private VisualElement BuildLabel(string text, int fontSize = 12, float extraWidth = 0)
    {
        var label = UITFactory.CreateLabel(text, fontSize, UITTheme.Text.Primary, m_font, TextAnchor.MiddleRight);
        const float LabelWidth = 50;
        label.style.width = LabelWidth + extraWidth;
        label.style.minWidth = LabelWidth + extraWidth;
        label.style.maxWidth = LabelWidth + extraWidth;
        label.style.flexGrow = 0;
        label.style.flexShrink = 0;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginRight = 6;
        return label;
    }
    
    private VisualElement BuildRow(EntryData data, HashSet<UITSlider> charSliders)
    {
        var row = BuildRow();

        // Label
        row.Add(BuildLabel(data.Key.GetLabel()));

        // Checkbox
        var checkbox = UITFactory.CreateCheckbox(
            data.Enabled
                ? UITFactory.CheckboxState.Checked
                : UITFactory.CheckboxState.Default,
            m_font);
        checkbox.RegisterCallback<ClickEvent>(evt => OnEntryEnabledChanged?.Invoke(data.Key, !data.Enabled));
        row.Add(checkbox);

        // Slider
        const float NotchSize = 0.1f;
        var sl = new UITSlider();
        sl.Setup(0f, data.MaxValue, m_font, value => $"{value:0.00}");
        sl.SetValue(data.Value);
        sl.SetNotchSize(data.Key is EntryKey.Global ? 0.05f : NotchSize);
        sl.SetStep(NotchSize);

        bool handleAll(float value)
        {
            if (data.Key is not EntryKey.All)
                return false;

            var start = Mathf.Min(data.Value, value) - 0.001f;
            var end = Mathf.Max(data.Value, value) + 0.001f;
            data.Value = value;
            foreach (var charSlider in charSliders)
                if (charSlider.Value >= start && charSlider.Value <= end)
                    charSlider.SetValueAndNotify(value);
            return true;
        }

        sl.OnValueChanged += value =>
        {
            handleAll(value);
            OnEntryValueChanged?.Invoke(data.Key, value, false);
        };
        sl.OnValueCommitted += value => OnEntryValueChanged?.Invoke(data.Key, value, !handleAll(value));

        row.Add(sl);

        if (data.Key is EntryKey.Char)
            charSliders.Add(sl);

        return row;
    }

    private void EnsureBuilt()
    {
        if (m_panel != null) return;

        m_font = UITRuntime.ResolveJapaneseFont(out var fontNames);
        m_settings = UITRuntime.CreatePanelSettings();
        m_doc = UITRuntime.AttachDocument(gameObject, m_settings);
        m_root = m_doc.rootVisualElement;
        m_root.style.flexGrow = 1;
        m_root.focusable = false;

        m_panel = UITFactory.CreatePanel();
        m_panel.style.position = Position.Absolute;
        m_panel.style.right = 16;
        m_panel.style.top = 20;
        m_panel.style.flexShrink = 1;
        m_panel.style.width = 280;
        m_panel.style.paddingTop = 12;
        m_panel.style.paddingRight = 12;
        m_panel.style.paddingBottom = 10;
        m_panel.style.paddingLeft = 12;
        m_root.Add(m_panel);

        // Header
        var headerRow = UITFactory.CreateRow();
        headerRow.style.height = 22;
        headerRow.style.marginBottom = 6;
        headerRow.style.alignItems = Align.Center;
        m_panel.Add(headerRow);

        var headerText = UITFactory.CreateLabel("スカート風力", 13, UITTheme.Text.Accent, m_font, TextAnchor.MiddleLeft);
        headerText.style.flexGrow = 1;
        headerRow.Add(headerText);

        var closeBtn = UITFactory.CreateButton("×", () => OnCloseClicked?.Invoke(), 16, m_font);
        closeBtn.style.width = 22;
        closeBtn.style.height = 22;
        headerRow.Add(closeBtn);

        var scrollView = new ScrollView(ScrollViewMode.Vertical)
        {
            style =
            {
                flexGrow = 1,
                marginBottom = 6,
            }
        };
        m_panel.Add(scrollView);

        m_listContainer = UITFactory.CreateColumn();
        m_listContainer.style.flexGrow = 1;
        scrollView.contentContainer.Add(m_listContainer);

        m_panel.style.display = DisplayStyle.None;
    }

    private static void SetBorderRadius(VisualElement v, float r)
    {
        v.style.borderTopLeftRadius = r;
        v.style.borderTopRightRadius = r;
        v.style.borderBottomLeftRadius = r;
        v.style.borderBottomRightRadius = r;
    }

    private static void SetBorderAll(VisualElement v, Color color, float width)
    {
        v.style.borderTopColor = color;
        v.style.borderRightColor = color;
        v.style.borderBottomColor = color;
        v.style.borderLeftColor = color;
        v.style.borderTopWidth = width;
        v.style.borderRightWidth = width;
        v.style.borderBottomWidth = width;
        v.style.borderLeftWidth = width;
    }

    private void OnDestroy()
    {
        if (m_settings != null)
        {
            Destroy(m_settings);
            m_settings = null;
        }
    }
}
