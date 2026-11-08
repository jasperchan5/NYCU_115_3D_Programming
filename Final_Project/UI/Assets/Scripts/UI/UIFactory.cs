using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FunctionSelection
{
    /// 
    /// 用程式建立 uGUI 元件（不需要 prefab）。所有選單都由這些函式組出來。
    /// 
    public static class UIFactory
    {
        public static readonly Color PanelColor = new Color(0.06f, 0.08f, 0.12f, 0.82f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.22f, 0.32f, 0.95f);
        public static readonly Color ButtonHover = new Color(0.95f, 0.72f, 0.25f, 1f);
        public static readonly Color Accent = new Color(1f, 0.8f, 0.35f);

        static Font font;
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float margin = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
        }

        /// 以 anchor（0–1）放置，pivot 同 anchor。
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset = default)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleCenter,
                                 Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.fontStyle = style;
            t.color = color ?? Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, int padding = 0,
                                                 TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = align;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return h;
        }

        public static LayoutElement Size(Component c, float height, float width = -1f)
        {
            if (!c.gameObject.TryGetComponent(out LayoutElement le)) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            if (width > 0) { le.preferredWidth = width; le.minWidth = width; }
            return le;
        }

        public static Button Button(Transform parent, string label, Action onClick, int fontSize = 30)
        {
            var rt = Rect("Button_" + label, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = ButtonColor;
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ButtonHover;
            colors.selectedColor = ButtonHover;          // 鍵盤 / 手把選到時也會亮起
            colors.pressedColor = new Color(1f, 0.55f, 0.15f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => { GameApp.I?.PlayClick(); onClick?.Invoke(); });
            rt.gameObject.AddComponent<ButtonFx>();

            var txt = Label(rt, label, fontSize, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            Stretch(txt.rectTransform);
            return btn;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChange)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            var s = go.GetComponent<Slider>();
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            foreach (var img in go.GetComponentsInChildren<Image>())
            {
                if (img.name == "Background") img.color = new Color(1f, 1f, 1f, 0.25f);
                else if (img.name == "Fill") img.color = Accent;
                else if (img.name == "Handle") img.color = Color.white;
            }
            return s;
        }

        public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChange)
        {
            var go = DefaultControls.CreateToggle(new DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Toggle>();
            t.isOn = value;
            t.onValueChanged.AddListener(v => onChange?.Invoke(v));
            var txt = go.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = label;
                txt.font = Font;
                txt.fontSize = 26;
                txt.color = Color.white;
            }
            foreach (var img in go.GetComponentsInChildren<Image>())
                if (img.name == "Checkmark") img.color = Accent;
            return t;
        }

        public static Dropdown Dropdown(Transform parent, List<string> options, int value, Action<int> onChange)
        {
            var go = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            var d = go.GetComponent<Dropdown>();
            d.ClearOptions();
            d.AddOptions(options);
            d.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, options.Count - 1)));
            d.RefreshShownValue();
            d.onValueChanged.AddListener(v => onChange?.Invoke(v));
            foreach (var txt in go.GetComponentsInChildren<Text>(true))
            {
                txt.font = Font;
                txt.fontSize = 24;
                txt.color = Color.black;
            }
            // 下拉清單的高度放大一點，選項才看得清楚
            var template = d.template;
            if (template != null) template.sizeDelta = new Vector2(template.sizeDelta.x, 320f);
            return d;
        }

        /// 一列「標籤 + 控制項」，用在設定頁。
        public static RectTransform Row(Transform parent, string label, float height = 56f)
        {
            var row = Rect("Row_" + label, parent);
            Size(row, height);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            var txt = Label(row, label, 28, TextAnchor.MiddleLeft);
            Size(txt, height, 300f);
            return row;
        }

        /// 把控制項放進一列，並撐滿剩下的寬度。
        public static void Fill(Component c, float height = 40f)
        {
            if (!c.gameObject.TryGetComponent(out LayoutElement le)) le = c.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredHeight = height;
        }
    }

    /// 
    /// 按鈕的小動畫：滑鼠移上或被鍵盤選到時放大，並播放提示音。
    /// 
    public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        float target = 1f;
        bool hovered, selected;

        public void OnPointerEnter(PointerEventData e) { hovered = true; Refresh(true); }
        public void OnPointerExit(PointerEventData e) { hovered = false; Refresh(false); }
        public void OnSelect(BaseEventData e) { selected = true; Refresh(true); }
        public void OnDeselect(BaseEventData e) { selected = false; Refresh(false); }

        void Refresh(bool entering)
        {
            target = hovered || selected ? 1.07f : 1f;
            if (entering) GameApp.I?.PlayHover();
        }

        void OnDisable()
        {
            hovered = selected = false;
            target = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
