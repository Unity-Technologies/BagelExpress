using System;
using System.Globalization;
using Unity.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Home page: collect the register, tap the storefront to bake (multi-touch
    /// friendly: every finger spawns its own pop), chat with Begu-chan, and a
    /// live event countdown. Pure UI logic, works in UI Builder and editor windows.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Managers")]
    public partial class HomePaneManager : BakeryPane
    {
        public static readonly string ussClassName = "be-home";
        public static readonly string popUssClassName = "be-pop";
        public static readonly UniqueStyleString storeSquishUssClassName = new("be-store--squish");

        // Begu-chan's lines: entries of the BagelStrings table.
        static readonly string[] s_LineKeys =
        {
            "home.line.welcome", "home.line.tap", "home.line.honey",
            "home.line.upgrade", "home.line.lucky-bag", "home.line.irasshai"
        };

        Button m_CollectButton;
        VisualElement m_Storefront;
        VisualElement m_PopLayer;
        VisualElement m_Mascot;
        Label m_SpeechText;
        Label m_Countdown;
        Button m_EventBanner;
        Label m_StoreName;
        Label m_StoreNameJapanese;

        int m_LineIndex;
        readonly LocalizedString m_Line = BagelLocalization.String(s_LineKeys[0]);
        bool m_Bound;
        long m_EventEndsAtSeconds;
        IVisualElementScheduledItem m_CountdownItem;
        IVisualElementScheduledItem m_UnsquishItem;

        public Button collectButton => m_CollectButton ??= this.Q<Button>("collect-button");
        public VisualElement storefront => m_Storefront ??= this.Q<VisualElement>("storefront");
        public VisualElement popLayer => m_PopLayer ??= this.Q<VisualElement>("pop-layer");
        public VisualElement mascot => m_Mascot ??= this.Q<VisualElement>("mascot");
        public Label speechText => m_SpeechText ??= this.Q<Label>("speech-text");
        public Label countdown => m_Countdown ??= this.Q<Label>("event-countdown");
        public Button eventBanner => m_EventBanner ??= this.Q<Button>("event-banner");
        public Label storeName => m_StoreName ??= this.Q<Label>("store-name");
        public Label storeNameJapanese => m_StoreNameJapanese ??= this.Q<Label>("store-name-jp");

        public HomePaneManager()
        {
            AddToClassList(ussClassName);
            // Event ends 2 days, 14 hours from first launch.
            m_EventEndsAtSeconds = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds + 2 * 86400 + 14 * 3600 + 22 * 60;
        }

        protected override void OnAttached()
        {
            if (!m_Bound)
            {
                m_Bound = true;
                collectButton?.RegisterCallback<ClickEvent>(_ => Collect());
                storefront?.RegisterCallback<PointerDownEvent>(OnStorefrontPointerDown);
                mascot?.RegisterCallback<PointerDownEvent>(_ => NextLine());
                eventBanner?.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!BakeryAppManager.IsPinned(this))
                        app?.GoTo(AppPage.LuckyBag);
                });
                // The bubble is bound to a LocalizedString: changing its entry or the language updates it.
                speechText?.SetBinding("text", m_Line);
            }

            m_CountdownItem ??= schedule.Execute(UpdateCountdown).Every(1000);
            UpdateCountdown();
            RefreshStoreSign();
        }

        protected override void OnStateChanged(BakeryState oldState, BakeryState newState) => RefreshStoreSign();
        protected override void OnLocaleChanged() => RefreshStoreSign();

        void RefreshStoreSign()
        {
            if (state == null || state.shopStates.Count == 0 || storeName == null)
                return;
            var shop = state.shopStates[0];
            storeName.text = shop.displayName?.ToUpperInvariant();
            if (storeNameJapanese != null)
                storeNameJapanese.text = shop.secondaryName;
        }

        public void Collect()
        {
            if (state == null)
                return;

            var amount = state.Collect();
            if (amount <= 0)
            {
                BagelSound.Play(collectButton, "fail");
                Toast(BagelLocalization.Get("toast.register-empty"));
                return;
            }

            BagelSound.Play(collectButton, "collect");

            SpawnPop(collectButton, collectButton.contentRect.center + new Vector2(0f, -26f), amount);
            if (app != null)
                app.FlyCurrency(collectButton, CurrencyKind.Coins, Mathf.Clamp((int)(amount / 60), 4, 10));
        }

        void OnStorefrontPointerDown(PointerDownEvent evt) => TapBake((Vector2)evt.localPosition);

        /// <summary>One tap on the storefront at <paramref name="localPosition"/>: bake, pop "+N" there, squish.</summary>
        public void TapBake(Vector2 localPosition)
        {
            if (state == null || storefront == null)
                return;

            var amount = state.TapBake();
            SpawnPop(storefront, localPosition, amount);
            BagelSound.Play(storefront, "bake");

            storefront.AddToClassList(storeSquishUssClassName);
            m_UnsquishItem ??= storefront.schedule.Execute(() => storefront.RemoveFromClassList(storeSquishUssClassName));
            m_UnsquishItem.ExecuteLater(110);
        }

        /// <summary>
        /// "+6" label that floats up and fades. The motion is the CoinPop UIAnimationClip,
        /// assigned to .be-pop in USS: it plays once as soon as the element is styled.
        /// </summary>
        void SpawnPop(VisualElement relativeTo, Vector2 localPosition, long amount)
        {
            if (popLayer == null || relativeTo == null)
                return;

            var position = popLayer.WorldToLocal(relativeTo.LocalToWorld(localPosition));
            var pop = new Label("+" + amount.ToString("N0", CultureInfo.InvariantCulture)) { pickingMode = PickingMode.Ignore };
            pop.AddToClassList(popUssClassName);
            pop.style.left = position.x - 30f;
            pop.style.top = position.y - 24f;
            popLayer.Add(pop);
            pop.schedule.Execute(pop.RemoveFromHierarchy).ExecuteLater(850);
        }

        void NextLine()
        {
            BagelSound.Play(mascot, "poke");
            m_LineIndex = (m_LineIndex + 1) % s_LineKeys.Length;
            m_Line.TableEntryReference = s_LineKeys[m_LineIndex];
        }

        /// <summary>Countdown text written with the zero-allocation SetText span overload.</summary>
        void UpdateCountdown()
        {
            if (countdown == null || countdown.panel == null)
                return;

            var remaining = Math.Max(0, m_EventEndsAtSeconds - (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds);
            var days = remaining / 86400;
            var hours = remaining / 3600 % 24;
            var minutes = remaining / 60 % 60;
            var seconds = remaining % 60;

            Span<char> buffer = stackalloc char[16];
            var length = 0;
            Append(buffer, ref length, days, 1);
            buffer[length++] = 'd';
            buffer[length++] = ' ';
            Append(buffer, ref length, hours, 2);
            buffer[length++] = ':';
            Append(buffer, ref length, minutes, 2);
            buffer[length++] = ':';
            Append(buffer, ref length, seconds, 2);
            countdown.SetText(buffer[..length]);
        }

        static void Append(Span<char> buffer, ref int length, long value, int minDigits)
        {
            value.TryFormat(buffer[length..], out var written, minDigits == 2 ? "00" : "0", CultureInfo.InvariantCulture);
            length += written;
        }
    }
}
